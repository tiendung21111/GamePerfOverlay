namespace GamePerfOverlay.Services;

public class HotkeyChordDetector
{
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_D = 0x44;
    private const int VK_M = 0x4D;

    private bool _ctrlDown;
    private bool _dDown;
    private bool _mDown;
    private bool _triggered;

    public bool ProcessKeyEvent(int vkCode, bool isDown)
    {
        if (vkCode == VK_CONTROL || vkCode == VK_LCONTROL || vkCode == VK_RCONTROL)
        {
            _ctrlDown = isDown;
        }
        else if (vkCode == VK_D)
        {
            _dDown = isDown;
        }
        else if (vkCode == VK_M)
        {
            _mDown = isDown;
        }

        if (_ctrlDown && _dDown && _mDown)
        {
            if (!_triggered)
            {
                _triggered = true;
                return true;
            }
        }
        else
        {
            _triggered = false;
        }

        return false;
    }
}
