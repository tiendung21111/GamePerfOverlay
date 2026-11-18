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
        SetTargetKeys(targetKeys ?? [0x11, 0x44, 0x4D]); // Default: Ctrl + D + M
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

    public bool ProcessKeyEvent(int vkCode, bool isDown)
    {
        int normalized = HotkeyHelper.NormalizeVkCode(vkCode);

        lock (_targetKeys)
        {
            if (_targetKeys.Count == 0) return false;

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
