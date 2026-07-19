using System.Text;
using Anity.Cli;
using Xunit;

namespace Anity.Cli.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CliLogFileCollection
{
    public const string Name = "CLI log file process state";
}

[Collection(CliLogFileCollection.Name)]
public sealed class CliLogFileTests
{
    [Fact]
    public void ParserPreservesStandardOutputSentinel()
    {
        Assert.Equal("-", CommandLineArgs.Parse(new[] { "-logFile", "-" }).LogFile);
    }

    [Theory]
    [InlineData("-logFile")]
    [InlineData("-logfile")]
    [InlineData("-LOGFILE")]
    [InlineData("--logFile")]
    public void StandardOutputSentinelStreamsLogWithoutCreatingDashFile(string switchName)
    {
        WithTemporaryCurrentDirectory(directory =>
        {
            var output = new StringWriter(new StringBuilder());
            var host = new CliHost(output);
            Assert.Equal(0, host.Run(new[] { switchName, "-", "-batchmode", "-quit" }));
            Assert.Equal(host.LogText, output.ToString());
            Assert.Contains("batchmode=1", output.ToString());
            Assert.False(File.Exists(Path.Combine(directory, "-")));
        });
    }

    [Fact]
    public void VersionStillFlushesStandardOutputSentinel()
    {
        var output = new TrackingWriter();
        Assert.Equal(0, new CliHost(output).Run(new[] { "-logFile", "-", "-version" }));
        Assert.True(output.WasFlushed);
        Assert.Contains("Anity CLI", output.ToString());
    }

    [Fact]
    public void RelativeLogFileReceivesCompleteLog()
    {
        WithTemporaryCurrentDirectory(directory =>
        {
            var host = new CliHost(TextWriter.Null);
            Assert.Equal(0, host.Run(new[] { "-logFile", "editor.log", "-batchmode", "-quit" }));
            Assert.Equal(host.LogText, File.ReadAllText(Path.Combine(directory, "editor.log")));
        });
    }

    [Fact]
    public void LogFileCreatesMissingParentDirectories()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "Logs", "Editor.log");
            var host = new CliHost(TextWriter.Null);
            Assert.Equal(0, host.Run(new[] { "-logFile", path, "-batchmode", "-quit" }));
            Assert.Equal(host.LogText, File.ReadAllText(path));
        });
    }

    [Fact]
    public void LogFileOverwritesPreviousSessionContent()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "Editor.log");
            File.WriteAllText(path, "stale");
            var host = new CliHost(TextWriter.Null);
            Assert.Equal(0, host.Run(new[] { "-logFile", path, "-version" }));
            Assert.Equal(host.LogText, File.ReadAllText(path));
            Assert.DoesNotContain("stale", File.ReadAllText(path));
        });
    }

    [Fact]
    public void HelpEarlyReturnStillWritesRequestedLogFile()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "help.log");
            var host = new CliHost(TextWriter.Null);
            Assert.Equal(0, host.Run(new[] { "-logFile", path, "-help" }));
            Assert.Equal(host.LogText, File.ReadAllText(path));
            Assert.Contains("-batchmode", File.ReadAllText(path));
        });
    }

    [Fact]
    public void ProjectPathFailureStillWritesRequestedLogFile()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "failure.log");
            var missing = Path.Combine(directory, "missing-project");
            var host = new CliHost(TextWriter.Null);
            Assert.Equal(1, host.Run(new[] { "-logFile", path, "-projectPath", missing }));
            Assert.Equal(host.LogText, File.ReadAllText(path));
            Assert.Contains("projectPath not found", File.ReadAllText(path));
        });
    }

    private static void WithTemporaryCurrentDirectory(Action<string> action)
    {
        var original = Directory.GetCurrentDirectory();
        WithTemporaryDirectory(directory =>
        {
            try
            {
                Directory.SetCurrentDirectory(directory);
                action(directory);
            }
            finally
            {
                Directory.SetCurrentDirectory(original);
            }
        });
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "anity-cli-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    private sealed class TrackingWriter : StringWriter
    {
        internal bool WasFlushed { get; private set; }
        public override void Flush()
        {
            WasFlushed = true;
            base.Flush();
        }
    }
}
