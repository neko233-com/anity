using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

[AddComponentMenu("UI/Toggle Group")]
public class ToggleGroup : UIBehaviour
{
    [SerializeField] private bool m_AllowSwitchOff;
    private readonly List<Toggle> m_Toggles = new();

    public bool allowSwitchOff
    {
        get => m_AllowSwitchOff;
        set => m_AllowSwitchOff = value;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureValidState();
    }

    private void ValidateToggleIsInGroup(Toggle toggle)
    {
        if (toggle == null || !m_Toggles.Contains(toggle))
            throw new ArgumentException($"Toggle {toggle} is not part of ToggleGroup {this}");
    }

    public void NotifyToggleOn(Toggle toggle, bool sendCallback = true)
    {
        ValidateToggleIsInGroup(toggle);
        for (var i = 0; i < m_Toggles.Count; i++)
        {
            if (m_Toggles[i] == toggle)
            {
                m_Toggles[i].Set(true, sendCallback, false);
            }
            else
            {
                m_Toggles[i].Set(false, sendCallback, false);
            }
        }
    }

    public void UnregisterToggle(Toggle toggle)
    {
        if (m_Toggles.Contains(toggle))
            m_Toggles.Remove(toggle);
    }

    public void RegisterToggle(Toggle toggle)
    {
        if (!m_Toggles.Contains(toggle))
            m_Toggles.Add(toggle);
    }

    public bool AnyTogglesOn()
    {
        return m_Toggles.Find(x => x.isOn) != null;
    }

    public IEnumerable<Toggle> ActiveToggles()
    {
        return m_Toggles.FindAll(x => x.isOn);
    }

    public Toggle ActiveToggle()
    {
        for (var i = 0; i < m_Toggles.Count; i++)
        {
            if (m_Toggles[i].isOn)
                return m_Toggles[i];
        }
        return null;
    }

    public void SetAllTogglesOff(bool sendCallback = true)
    {
        for (var i = 0; i < m_Toggles.Count; i++)
        {
            m_Toggles[i].Set(false, sendCallback, false);
        }
    }

    public void EnsureValidState()
    {
        if (!AnyTogglesOn() && !m_AllowSwitchOff && m_Toggles.Count > 0)
        {
            for (var i = 0; i < m_Toggles.Count; i++)
            {
                m_Toggles[i].Set(false, false, false);
            }
            m_Toggles[0].Set(true, true, false);
        }
    }
}
