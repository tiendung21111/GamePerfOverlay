using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePerfOverlay.Services;

public class HotkeyChordDetector
{
    private readonly HashSet<int> _targetKeys = new();
    private readonly HashSet<int> _pressedKeys = new();
    private bool _triggered;

    public HotkeyChordDetector(IEnumerable<int>? targetKeys = null)
    {
        SetTargetKeys(targetKeys ?? [HotkeyHelper.VK_MENU, 0x51]); // Default: Alt + Q
    }

    public void SetTargetKeys(IEnumerable<int> keys)
    {
        lock (_targetKeys)
        {
            _targetKeys.Clear();
            foreach (var k in keys)
            {
                _targetKeys.Add(HotkeyHelper.NormalizeVkCode(k));
            }
            _pressedKeys.Clear();
            _triggered = false;
        }
    }

    public IReadOnlyCollection<int> TargetKeys
    {
        get
        {
            lock (_targetKeys)
            {
                return _targetKeys.ToArray();
            }
        }
    }

    /// <summary>
    /// Processes key down / up events.
    /// Supports an optional physical key state delegate (e.g. via GetAsyncKeyState)
    /// to self-heal and unstick chords if Windows dropped a KeyUp event.
    /// </summary>
    public bool ProcessKeyEvent(int vkCode, bool isDown, Func<int, bool>? isPhysicalKeyDown = null)
    {
        int normalized = HotkeyHelper.NormalizeVkCode(vkCode);

        lock (_targetKeys)
        {
            if (_targetKeys.Count == 0) return false;

            // Self-heal: If physical key checker is provided, purge any keys that are physically released.
            // This prevents chords getting permanently stuck if Windows drops a KeyUp message.
            if (isPhysicalKeyDown != null)
            {
                _pressedKeys.RemoveWhere(k => !isPhysicalKeyDown(k));

                if (!_targetKeys.All(isPhysicalKeyDown))
                {
                    _triggered = false;
                }
            }

            if (isDown)
            {
                _pressedKeys.Add(normalized);

                if (_targetKeys.All(_pressedKeys.Contains))
                {
                    if (!_triggered)
                    {
                        _triggered = true;
                        return true;
                    }
                }
            }
            else
            {
                _pressedKeys.Remove(normalized);

                if (_targetKeys.Contains(normalized))
                {
                    _triggered = false;
                }
            }

            return false;
        }
    }
}
