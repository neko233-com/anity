using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Anity.Agent;

/// <summary>
/// Stores Agent credentials in the operating-system credential vault. Implementations must
/// fail closed; writing an unencrypted file is never an allowed fallback.
/// </summary>
public interface IAgentCredentialVault
{
    string BackendName { get; }
    bool IsAvailable { get; }
    Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default);
    Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default);
}

/// <summary>Creates the native credential vault for the current desktop platform.</summary>
public static class AgentCredentialVault
{
    internal const int MaxCredentialBytes = 2048;

    public static IAgentCredentialVault CreateSystemDefault()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOsAgentCredentialVault();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsAgentCredentialVault();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxSecretServiceAgentCredentialVault();
        return new UnsupportedAgentCredentialVault();
    }

    internal static string ValidateCredentialId(string credentialId)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
            throw new ArgumentException("Credential id is required.", nameof(credentialId));
        if (credentialId.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(credentialId), "Credential id must be at most 128 characters.");
        foreach (char character in credentialId)
        {
            bool valid = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.' or '_' or '-';
            if (!valid)
                throw new ArgumentException(
                    "Credential id may only contain ASCII letters, digits, '.', '_' and '-'.",
                    nameof(credentialId));
        }
        return credentialId;
    }

    internal static byte[] ValidateSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Credential secret is required.", nameof(secret));
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(secret);
        if (bytes.Length > MaxCredentialBytes)
            throw new ArgumentOutOfRangeException(nameof(secret),
                $"Credential secret must be at most {MaxCredentialBytes} UTF-8 bytes.");
        return bytes;
    }
}

internal sealed class UnsupportedAgentCredentialVault : IAgentCredentialVault
{
    public string BackendName => "unsupported";
    public bool IsAvailable => false;

    public Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default)
        => Task.FromException(new PlatformNotSupportedException(
            "No secure Agent credential vault is available on this platform."));

    public Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default)
        => Task.FromException<string?>(new PlatformNotSupportedException(
            "No secure Agent credential vault is available on this platform."));

    public Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new PlatformNotSupportedException(
            "No secure Agent credential vault is available on this platform."));
}

internal sealed class MacOsAgentCredentialVault : IAgentCredentialVault
{
    private const string Service = "com.anity.agent";
    private const int Success = 0;
    private const int ItemNotFound = -25300;

    public string BackendName => "macOS Keychain";
    public bool IsAvailable => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string id = AgentCredentialVault.ValidateCredentialId(credentialId);
        byte[] service = Encoding.UTF8.GetBytes(Service);
        byte[] account = Encoding.UTF8.GetBytes(id);
        byte[] value = AgentCredentialVault.ValidateSecret(secret);
        IntPtr item = IntPtr.Zero;
        IntPtr passwordData = IntPtr.Zero;
        int status = SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
            out _, out passwordData, out item);
        try
        {
            if (status == Success)
            {
                status = SecKeychainItemModifyAttributesAndData(
                    item, IntPtr.Zero, (uint)value.Length, value);
            }
            else if (status == ItemNotFound)
            {
                status = SecKeychainAddGenericPassword(
                    IntPtr.Zero, (uint)service.Length, service, (uint)account.Length,
                    account, (uint)value.Length, value, out item);
            }
            ThrowIfError(status, "store");
            return Task.CompletedTask;
        }
        finally
        {
            if (passwordData != IntPtr.Zero)
                _ = SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            if (item != IntPtr.Zero) CFRelease(item);
            CryptographicOperations.ZeroMemory(value);
        }
    }

    public Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string id = AgentCredentialVault.ValidateCredentialId(credentialId);
        byte[] service = Encoding.UTF8.GetBytes(Service);
        byte[] account = Encoding.UTF8.GetBytes(id);
        int status = SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
            out uint length, out IntPtr data, out IntPtr item);
        try
        {
            if (status == ItemNotFound) return Task.FromResult<string?>(null);
            ThrowIfError(status, "retrieve");
            if (length > AgentCredentialVault.MaxCredentialBytes)
                throw new InvalidDataException("Stored Agent credential exceeds the supported limit.");
            byte[] bytes = new byte[length];
            try
            {
                if (length > 0) Marshal.Copy(data, bytes, 0, checked((int)length));
                return Task.FromResult<string?>(new UTF8Encoding(false, true).GetString(bytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        finally
        {
            if (data != IntPtr.Zero) _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    public Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string id = AgentCredentialVault.ValidateCredentialId(credentialId);
        byte[] service = Encoding.UTF8.GetBytes(Service);
        byte[] account = Encoding.UTF8.GetBytes(id);
        int status = SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
            out _, out IntPtr data, out IntPtr item);
        try
        {
            if (status == ItemNotFound) return Task.FromResult(false);
            ThrowIfError(status, "find before delete");
            ThrowIfError(SecKeychainItemDelete(item), "delete");
            return Task.FromResult(true);
        }
        finally
        {
            if (data != IntPtr.Zero) _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    private static void ThrowIfError(int status, string operation)
    {
        if (status != Success)
            throw new InvalidOperationException(
                $"macOS Keychain failed to {operation} the Agent credential (OSStatus {status}).");
    }

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain, uint serviceNameLength, byte[] serviceName,
        uint accountNameLength, byte[] accountName, uint passwordLength,
        byte[] passwordData, out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName,
        uint accountNameLength, byte[] accountName, out uint passwordLength,
        out IntPtr passwordData, out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef, IntPtr attrList, uint length, byte[] data);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);
}

internal sealed class WindowsAgentCredentialVault : IAgentCredentialVault
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public string BackendName => "Windows Credential Manager";
    public bool IsAvailable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string target = Target(credentialId);
        byte[] bytes = AgentCredentialVault.ValidateSecret(secret);
        GCHandle handle = default;
        try
        {
            handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = handle.AddrOfPinnedObject(),
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Windows Credential Manager could not store the Agent credential.");
            return Task.CompletedTask;
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string target = Target(credentialId);
        if (!CredRead(target, CredTypeGeneric, 0, out IntPtr pointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return Task.FromResult<string?>(null);
            throw new Win32Exception(error,
                "Windows Credential Manager could not retrieve the Agent credential.");
        }
        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlobSize > AgentCredentialVault.MaxCredentialBytes)
                throw new InvalidDataException("Stored Agent credential exceeds the supported limit.");
            byte[] bytes = new byte[credential.CredentialBlobSize];
            try
            {
                if (bytes.Length > 0)
                    Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                return Task.FromResult<string?>(new UTF8Encoding(false, true).GetString(bytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string target = Target(credentialId);
        if (CredDelete(target, CredTypeGeneric, 0)) return Task.FromResult(true);
        int error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound) return Task.FromResult(false);
        throw new Win32Exception(error,
            "Windows Credential Manager could not delete the Agent credential.");
    }

    private static string Target(string credentialId)
        => "Anity.Agent/" + AgentCredentialVault.ValidateCredentialId(credentialId);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}

internal sealed class LinuxSecretServiceAgentCredentialVault : IAgentCredentialVault
{
    public string BackendName => "Linux Secret Service";
    public bool IsAvailable => RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        && FindExecutable("secret-tool") is not null;

    public async Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default)
    {
        string id = AgentCredentialVault.ValidateCredentialId(credentialId);
        byte[] bytes = AgentCredentialVault.ValidateSecret(secret);
        CryptographicOperations.ZeroMemory(bytes);
        ProcessResult result = await RunSecretToolAsync(
            new[] { "store", "--label=Anity Agent API Key", "service", "Anity.Agent", "account", id },
            secret + "\n", cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Linux Secret Service could not store the Agent credential: {Sanitize(result.Error)}");
    }

    public async Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        string id = AgentCredentialVault.ValidateCredentialId(credentialId);
        ProcessResult result = await RunSecretToolAsync(
            new[] { "lookup", "service", "Anity.Agent", "account", id },
            null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1) return null;
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Linux Secret Service could not retrieve the Agent credential: {Sanitize(result.Error)}");
        string secret = result.Output.TrimEnd('\r', '\n');
        _ = AgentCredentialVault.ValidateSecret(secret);
        return secret;
    }

    public async Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        string id = AgentCredentialVault.ValidateCredentialId(credentialId);
        ProcessResult result = await RunSecretToolAsync(
            new[] { "clear", "service", "Anity.Agent", "account", id },
            null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1) return false;
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Linux Secret Service could not delete the Agent credential: {Sanitize(result.Error)}");
        return true;
    }

    private static async Task<ProcessResult> RunSecretToolAsync(
        string[] arguments, string? standardInput, CancellationToken cancellationToken)
    {
        string executable = FindExecutable("secret-tool")
            ?? throw new PlatformNotSupportedException(
                "secret-tool is required to use Linux Secret Service for Agent credentials.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Unable to start secret-tool.");
        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        });
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessResult(process.ExitCode, await stdout.ConfigureAwait(false),
            await stderr.ConfigureAwait(false));
    }

    private static string? FindExecutable(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (string directory in path.Split(Path.PathSeparator))
        {
            try
            {
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static string Sanitize(string error)
    {
        string value = (error ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 512 ? value : value.Substring(0, 512);
    }

    private readonly struct ProcessResult
    {
        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }

        public ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }
    }
}

internal static class CryptographicOperations
{
    public static void ZeroMemory(byte[] buffer)
    {
        if (buffer is null) return;
        Array.Clear(buffer, 0, buffer.Length);
    }
}
