using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>IMGUI GUI / GUIUtility interactive controls — ≥12 cases.</summary>
public class ImguiTests
{
    public ImguiTests()
    {
        GUI.enabled = true;
        GUI.changed = false;
        GUI.hotControl = 0;
        GUI.keyboardControl = 0;
        Event.current = new Event { type = EventType.Repaint };
    }

    [Fact]
    public void Skin_DefaultNonNull()
    {
        Assert.NotNull(GUI.skin);
        Assert.NotNull(GUI.skin.button);
        Assert.NotNull(GUI.skin.label);
    }

    [Fact]
    public void Color_RoundTrip()
    {
        GUI.color = Color.red;
        Assert.Equal(Color.red, GUI.color);
        GUI.contentColor = Color.green;
        GUI.backgroundColor = Color.blue;
        Assert.Equal(Color.green, GUI.contentColor);
        Assert.Equal(Color.blue, GUI.backgroundColor);
    }

    [Fact]
    public void Button_Click_ReturnsTrue()
    {
        var rect = new Rect(10, 10, 100, 30);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(50, 20),
            clickCount = 1
        };
        Assert.False(GUI.Button(rect, "Go"));
        Event.current = new Event
        {
            type = EventType.MouseUp,
            button = 0,
            mousePosition = new Vector2(50, 20),
            clickCount = 1
        };
        Assert.True(GUI.Button(rect, "Go"));
        Assert.True(GUI.changed);
    }

    [Fact]
    public void Button_Miss_ReturnsFalse()
    {
        var rect = new Rect(10, 10, 100, 30);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(500, 500)
        };
        Assert.False(GUI.Button(rect, "x"));
        Event.current = new Event
        {
            type = EventType.MouseUp,
            button = 0,
            mousePosition = new Vector2(500, 500)
        };
        Assert.False(GUI.Button(rect, "x"));
    }

    [Fact]
    public void Button_Disabled_NoClick()
    {
        GUI.enabled = false;
        var rect = new Rect(0, 0, 50, 50);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(10, 10)
        };
        Assert.False(GUI.Button(rect, "d"));
        Event.current = new Event
        {
            type = EventType.MouseUp,
            button = 0,
            mousePosition = new Vector2(10, 10)
        };
        Assert.False(GUI.Button(rect, "d"));
        GUI.enabled = true;
    }

    [Fact]
    public void Toggle_FlipsOnClick()
    {
        var rect = new Rect(0, 0, 40, 20);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(5, 5)
        };
        bool v = GUI.Toggle(rect, false, "t");
        Assert.True(v);
        Assert.True(GUI.changed);
    }

    [Fact]
    public void TextField_AppendsCharacter()
    {
        var rect = new Rect(0, 0, 200, 20);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(10, 10)
        };
        string t = GUI.TextField(rect, "hi");
        Assert.Equal("hi", t);

        Event.current = new Event
        {
            type = EventType.KeyDown,
            character = '!',
            keyCode = KeyCode.None
        };
        t = GUI.TextField(rect, t);
        Assert.Equal("hi!", t);
    }

    [Fact]
    public void TextField_Backspace()
    {
        var rect = new Rect(0, 0, 200, 20);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(5, 5)
        };
        GUI.TextField(rect, "ab");
        Event.current = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Backspace,
            character = '\0'
        };
        string t = GUI.TextField(rect, "ab");
        Assert.Equal("a", t);
    }

    [Fact]
    public void TextField_MaxLength()
    {
        var rect = new Rect(0, 0, 100, 20);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            mousePosition = new Vector2(1, 1),
            button = 0
        };
        GUI.TextField(rect, "xy", 3);
        Event.current = new Event { type = EventType.KeyDown, character = 'z', keyCode = KeyCode.None };
        string t = GUI.TextField(rect, "xy", 3);
        Assert.Equal("xyz", t);
        Event.current = new Event { type = EventType.KeyDown, character = 'w', keyCode = KeyCode.None };
        t = GUI.TextField(rect, t, 3);
        Assert.Equal("xyz", t);
    }

    [Fact]
    public void HorizontalSlider_Drag()
    {
        var rect = new Rect(0, 0, 100, 20);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(75, 10)
        };
        float v = GUI.HorizontalSlider(rect, 0f, 0f, 100f);
        Assert.InRange(v, 70f, 80f);
        Assert.True(GUI.changed);
    }

    [Fact]
    public void VerticalSlider_Drag()
    {
        var rect = new Rect(0, 0, 20, 100);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(10, 25)
        };
        float v = GUI.VerticalSlider(rect, 0f, 0f, 100f);
        Assert.InRange(v, 20f, 30f);
    }

    [Fact]
    public void BeginEndGroup_RestoresState()
    {
        GUI.color = Color.white;
        GUI.enabled = true;
        GUI.BeginGroup(new Rect(0, 0, 10, 10));
        GUI.color = Color.red;
        GUI.enabled = false;
        GUI.EndGroup();
        Assert.Equal(Color.white, GUI.color);
        Assert.True(GUI.enabled);
    }

    [Fact]
    public void Window_InvokesFunc()
    {
        int called = 0;
        var r = GUI.Window(1, new Rect(0, 0, 100, 100), id =>
        {
            called++;
            Assert.Equal(1, id);
        }, "Win");
        Assert.Equal(1, called);
        Assert.Equal(100, r.width);
    }

    [Fact]
    public void GetControlID_Increments()
    {
        int a = GUI.GetControlID(FocusType.Passive);
        int b = GUI.GetControlID(FocusType.Passive);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GUIUtility_HotControl_AndMatrix()
    {
        GUIUtility.hotControl = 7;
        Assert.Equal(7, GUIUtility.hotControl);
        GUIUtility.PushMatrix();
        GUIUtility.PopMatrix();
        Assert.Equal(1f, GUIUtility.pixelsPerPoint);
        Assert.Equal(new Vector2(1, 2), GUIUtility.GUIToScreenPoint(new Vector2(1, 2)));
    }

    [Fact]
    public void Toolbar_ClampsSelection()
    {
        int s = GUI.Toolbar(new Rect(0, 0, 200, 20), 5, new[] { "A", "B", "C" });
        Assert.Equal(2, s);
        s = GUI.Toolbar(new Rect(0, 0, 200, 20), -1, new[] { "A", "B" });
        Assert.Equal(0, s);
    }

    [Fact]
    public void SetNextControlName_AndFocus()
    {
        GUI.SetNextControlName("search");
        Assert.Equal("search", GUI.GetNameOfFocusedControl());
        GUI.FocusControl("search");
    }
}
