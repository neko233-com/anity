using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class InputField : Selectable, IPointerClickHandler, ISubmitHandler, IUpdateSelectedHandler
{
    public enum ContentType
    {
        Standard,
        Autocorrected,
        IntegerNumber,
        DecimalNumber,
        Alphanumeric,
        Name,
        EmailAddress,
        Password,
        Pin,
        Custom
    }

    public enum InputType
    {
        Standard,
        AutoCorrect,
        Password
    }

    public enum LineType
    {
        SingleLine,
        MultiLineSubmit,
        MultiLineNewline,
        MultiLine
    }

    public enum CharacterValidation
    {
        None,
        Integer,
        Decimal,
        Alphanumeric,
        Name,
        EmailAddress
    }

    private string _text = string.Empty;
    private Graphic? _textComponent;
    private Graphic? _placeholder;
    private ContentType _contentType = ContentType.Standard;
    private InputType _inputType = InputType.Standard;
    private LineType _lineType = LineType.SingleLine;
    private CharacterValidation _characterValidation = CharacterValidation.None;
    private int _characterLimit;
    private char _asteriskChar = '*';
    private bool _readOnly;
    private int _caretPosition;
    private int _selectionAnchorPosition;
    private int _selectionFocusPosition;
    private bool _isFocused;
    private float _caretBlinkRate = 0.85f;
    private int _caretWidth = 1;
    private Color _selectionColor = new Color(0.65882355f, 0.8156863f, 1f, 0.7529412f);
    private bool _shouldHideMobileInput;

    private InputFieldSubmitEvent _onEndEdit = new();
    private InputFieldChangeEvent _onValueChanged = new();
    private InputFieldSubmitEvent _onSubmit = new();

    public string text
    {
        get => _text;
        set
        {
            var newText = value ?? string.Empty;
            newText = ClampAndValidate(newText);
            if (_text != newText)
            {
                _text = newText;
                _caretPosition = _text.Length;
                _selectionAnchorPosition = _caretPosition;
                _selectionFocusPosition = _caretPosition;
                UpdateLabel();
                _onValueChanged?.Invoke(_text);
            }
        }
    }

    public bool isFocused => _isFocused;
    public int caretPosition => _caretPosition;
    public int selectionAnchorPosition => _selectionAnchorPosition;
    public int selectionFocusPosition => _selectionFocusPosition;
    public int selectionStringAnchorPosition => _selectionAnchorPosition;
    public int selectionStringFocusPosition => _selectionFocusPosition;

    public Graphic? textComponent
    {
        get => _textComponent;
        set => _textComponent = value;
    }

    public Graphic? placeholder
    {
        get => _placeholder;
        set => _placeholder = value;
    }

    public ContentType contentType
    {
        get => _contentType;
        set
        {
            if (_contentType != value)
            {
                _contentType = value;
                EnforceContentType();
            }
        }
    }

    public LineType lineType
    {
        get => _lineType;
        set => _lineType = value;
    }

    public InputType inputType
    {
        get => _inputType;
        set => _inputType = value;
    }

    public CharacterValidation characterValidation
    {
        get => _characterValidation;
        set => _characterValidation = value;
    }

    public int characterLimit
    {
        get => _characterLimit;
        set => _characterLimit = value;
    }

    public char asteriskChar
    {
        get => _asteriskChar;
        set => _asteriskChar = value;
    }

    public bool readOnly
    {
        get => _readOnly;
        set => _readOnly = value;
    }

    public float caretBlinkRate
    {
        get => _caretBlinkRate;
        set => _caretBlinkRate = value;
    }

    public int caretWidth
    {
        get => _caretWidth;
        set => _caretWidth = value;
    }

    public Color selectionColor
    {
        get => _selectionColor;
        set => _selectionColor = value;
    }

    public bool shouldHideMobileInput
    {
        get => _shouldHideMobileInput;
        set => _shouldHideMobileInput = value;
    }

    public bool multiLine => _lineType != LineType.SingleLine;

    public InputFieldSubmitEvent onEndEdit
    {
        get => _onEndEdit;
        set => _onEndEdit = value;
    }

    public InputFieldChangeEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    public InputFieldSubmitEvent onSubmit
    {
        get => _onSubmit;
        set => _onSubmit = value;
    }

    private void EnforceContentType()
    {
        switch (_contentType)
        {
            case ContentType.Standard:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.None;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Autocorrected:
                _inputType = InputType.AutoCorrect;
                _characterValidation = CharacterValidation.None;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.IntegerNumber:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Integer;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.DecimalNumber:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Decimal;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Alphanumeric:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Alphanumeric;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Name:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Name;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.EmailAddress:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.EmailAddress;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Password:
                _inputType = InputType.Password;
                _characterValidation = CharacterValidation.None;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Pin:
                _inputType = InputType.Password;
                _characterValidation = CharacterValidation.Integer;
                _lineType = LineType.SingleLine;
                break;
        }
    }

    private string ClampAndValidate(string input)
    {
        if (_characterLimit > 0 && input.Length > _characterLimit)
        {
            input = input.Substring(0, _characterLimit);
        }

        return _characterValidation switch
        {
            CharacterValidation.Integer => ValidateInteger(input),
            CharacterValidation.Decimal => ValidateDecimal(input),
            CharacterValidation.Alphanumeric => ValidateAlphanumeric(input),
            CharacterValidation.Name => ValidateName(input),
            CharacterValidation.EmailAddress => ValidateEmail(input),
            _ => input
        };
    }

    private static string ValidateInteger(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (int.TryParse(input, out _)) return input;
        var result = string.Empty;
        foreach (var c in input)
        {
            if (char.IsDigit(c) || (result.Length == 0 && c == '-'))
            {
                result += c;
            }
        }

        return result;
    }

    private static string ValidateDecimal(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (float.TryParse(input, out _)) return input;
        var result = string.Empty;
        var hasDot = false;
        foreach (var c in input)
        {
            if (char.IsDigit(c) || (result.Length == 0 && c == '-'))
            {
                result += c;
            }
            else if (c == '.' && !hasDot)
            {
                result += c;
                hasDot = true;
            }
        }

        return result;
    }

    private static string ValidateAlphanumeric(string input)
    {
        var result = string.Empty;
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                result += c;
            }
        }

        return result;
    }

    private static string ValidateName(string input)
    {
        var result = string.Empty;
        var lastWasSpace = false;
        foreach (var c in input)
        {
            if (char.IsLetter(c) || c == ' ' || c == '-')
            {
                if (c == ' ' && lastWasSpace) continue;
                result += c;
                lastWasSpace = c == ' ';
            }
        }

        return result;
    }

    private static string ValidateEmail(string input)
    {
        var result = string.Empty;
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c) || c == '@' || c == '.' || c == '_' || c == '-')
            {
                result += c;
            }
        }

        return result;
    }

    public void ActivateInputField()
    {
        if (!interactable || !IsActive()) return;
        _isFocused = true;
        Select();
    }

    public void DeactivateInputField()
    {
        _isFocused = false;
        _onEndEdit?.Invoke(_text);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        ActivateInputField();
    }

    public new void OnSubmit(BaseEventData eventData)
    {
        _ = eventData;
        if (!IsInteractable()) return;
        _onSubmit?.Invoke(_text);
        _onEndEdit?.Invoke(_text);
    }

    public new void OnUpdateSelected(BaseEventData eventData)
    {
        _ = eventData;
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        ActivateInputField();
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        DeactivateInputField();
    }

    public void SetTextWithoutNotify(string input)
    {
        var newText = ClampAndValidate(input ?? string.Empty);
        if (_text != newText)
        {
            _text = newText;
            _caretPosition = _text.Length;
            _selectionAnchorPosition = _caretPosition;
            _selectionFocusPosition = _caretPosition;
            UpdateLabel();
        }
    }

    public void ForceLabelUpdate()
    {
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_textComponent is Text text)
        {
            text.text = _inputType == InputType.Password ? new string(_asteriskChar, _text.Length) : _text;
        }

        if (_placeholder is not null)
        {
            _placeholder.enabled = string.IsNullOrEmpty(_text);
        }
    }

    public void ProcessEvent(Event e)
    {
        _ = e;
    }
}

[Serializable]
public class InputFieldSubmitEvent
{
    public event Action<string>? Submit;

    public void Invoke(string text)
    {
        Submit?.Invoke(text);
    }
}

[Serializable]
public class InputFieldChangeEvent
{
    public event Action<string>? ValueChanged;

    public void Invoke(string text)
    {
        ValueChanged?.Invoke(text);
    }
}
