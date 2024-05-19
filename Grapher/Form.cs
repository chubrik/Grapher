namespace Chubrik.Grapher;

using System.Runtime.InteropServices;

/*

Action              Keyboard    Mouse

Move                WASD ←↑↓→   Left drag-n-drop
Zoom                R F  + -    Wheel rotate
Zoom to selected                Right drag-n-drop
Smooth              Shift       Right
X axis only         Ctrl
Y axis only         Alt
Set max exponent    K L
Set min exponent    < >
Set as default      Enter
Reset to default    Esc

*/

internal partial class Form : System.Windows.Forms.Form
{
    private readonly Grapher _grapher;

    public Form(Action<Grapher> onGrapher)
    {
        InitializeComponent();
        _grapher = new(PictureBox);
        onGrapher(_grapher);
    }

    private FormWindowState _lastWindowState = FormWindowState.Normal;

    private void Form_Resize(object sender, EventArgs e)
    {
        if (WindowState != _lastWindowState &&
            WindowState != FormWindowState.Minimized)
        {
            _lastWindowState = WindowState;
            _grapher.OnResize();
        }
    }

    private void Form_ResizeEnd(object sender, EventArgs e)
    {
        _grapher.OnResize();
    }

    private bool _isShiftPressed;
    private bool _isLeftMouseButtonPressed;
    private bool _isRightMouseButtonPressed;
    private bool _onlyX;
    private bool _onlyY;

    private bool Smooth => _isShiftPressed || _isRightMouseButtonPressed;

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(int nVirtKey);

    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_RMENU = 0xA5;

    private void Form_KeyDown(object sender, KeyEventArgs e)
    {
        _isShiftPressed = e.Shift;

        var isLeftCtrlPressed = GetKeyState(VK_LCONTROL) < 0;
        var isRightCtrlPressed = GetKeyState(VK_RCONTROL) < 0;
        var isRightAltPressed = GetKeyState(VK_RMENU) < 0;
        var isCtrlPressed = (isLeftCtrlPressed && !isRightAltPressed) || isRightCtrlPressed;

        _onlyX = isCtrlPressed && !e.Alt;
        _onlyY = !isCtrlPressed && e.Alt;

        if (e.Alt)
            e.Handled = true;

        switch (e.KeyCode)
        {
            case Keys.W:
            case Keys.Up:
                _grapher.OnMoveUp(Smooth);
                break;

            case Keys.S:
            case Keys.Down:
                _grapher.OnMoveDown(Smooth);
                break;

            case Keys.A:
            case Keys.Left:
                _grapher.OnMoveLeft(Smooth);
                break;

            case Keys.D:
            case Keys.Right:
                _grapher.OnMoveRight(Smooth);
                break;

            case Keys.R:
            case Keys.Oemplus:
                OnZoom(zoomIn: true);
                break;

            case Keys.F:
            case Keys.OemMinus:
                OnZoom(zoomIn: false);
                break;

            case Keys.Enter:
                _grapher.OnSetAsDefault();
                break;

            case Keys.Escape:
                _grapher.OnReset();
                break;

            case Keys.K:
                _grapher.OnMaxExp(_onlyY ? 0 : -1, _onlyX ? 0 : -1);
                break;

            case Keys.L:
                _grapher.OnMaxExp(_onlyY ? 0 : 1, _onlyX ? 0 : 1);
                break;

            case Keys.Oemcomma: // <
                _grapher.OnMinExp(_onlyY ? 0 : -1, _onlyX ? 0 : -1);
                break;

            case Keys.OemPeriod: // >
                _grapher.OnMinExp(_onlyY ? 0 : 1, _onlyX ? 0 : 1);
                break;
        }
    }

    private void Form_KeyUp(object sender, KeyEventArgs e)
    {
        _isShiftPressed = e.Shift;
        _onlyX = e.Control && !e.Alt;
        _onlyY = !e.Control && e.Alt;
    }

    private int _mouseDownX;
    private int _mouseDownY;
    private bool _isWaitingMouseUp = false;

    private void PictureBox_MouseDown(object sender, MouseEventArgs e)
    {
        switch (e.Button)
        {
            case MouseButtons.Left:
                _isLeftMouseButtonPressed = true;

                if (_isRightMouseButtonPressed)
                    _isWaitingMouseUp = false;
                else
                {
                    _mouseDownX = e.X;
                    _mouseDownY = e.Y;
                    _isWaitingMouseUp = true;
                }

                break;

            case MouseButtons.Right:
                _isRightMouseButtonPressed = true;

                if (_isLeftMouseButtonPressed)
                    _isWaitingMouseUp = false;
                else
                {
                    _mouseDownX = e.X;
                    _mouseDownY = e.Y;
                    _isWaitingMouseUp = true;
                }

                break;
        }
    }

    private void PictureBox_MouseUp(object sender, MouseEventArgs e)
    {
        switch (e.Button)
        {
            case MouseButtons.Left:
                _isLeftMouseButtonPressed = false;

                if (_isWaitingMouseUp)
                    _grapher.OnMove(
                        _onlyY ? 0 : e.X - _mouseDownX,
                        _onlyX ? 0 : e.Y - _mouseDownY);

                break;

            case MouseButtons.Right:
                _isRightMouseButtonPressed = false;

                if (_isWaitingMouseUp)
                {
                    var minX = Math.Min(e.X, _mouseDownX);
                    var maxX = Math.Max(e.X, _mouseDownX);
                    var minY = Math.Min(e.Y, _mouseDownY);
                    var maxY = Math.Max(e.Y, _mouseDownY);

                    if (_onlyX)
                        _grapher.OnRangeX(minX, maxX);
                    else
                    if (_onlyY)
                        _grapher.OnRangeY(minY, maxY);
                    else
                        _grapher.OnRange(minX, maxX, minY, maxY);
                }

                break;
        }

        _isWaitingMouseUp = false;
    }

    private int _mousePosX = -1;
    private int _mousePosY = -1;

    private void PictureBox_MouseMove(object sender, MouseEventArgs e)
    {
        _mousePosX = e.X;
        _mousePosY = e.Y;
    }

    private void PictureBox_MouseLeave(object sender, EventArgs e)
    {
        _mousePosX = -1;
        _mousePosY = -1;
    }

    private void PictureBox_Wheel(object sender, MouseEventArgs e)
    {
        if (e.Delta == 120) // Mouse wheel up
            OnZoom(zoomIn: true);
        else
        if (e.Delta == -120) // Mouse wheel down
            OnZoom(zoomIn: false);
    }

    private void OnZoom(bool zoomIn)
    {
        _grapher.OnZoom(
            smooth: Smooth,
            zoomIn: zoomIn,
            rawX: _onlyY ? null : _mousePosX,
            rawY: _onlyX ? null : _mousePosY);
    }
}
