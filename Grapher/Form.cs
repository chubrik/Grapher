namespace Grapher;

/*
 *  Mouse wheel rotate        - zoom
 *  Mouse wheel click         - linear/logarithmic scale
 *  WSAD, arrows, drag-n-drop - move
 *  Shift, RMB                - smooth
 *  LMB + RMB + drag-n-drop   - zoom to rectangle
 *  Ctrl                      - only horizontal
 *  Alt                       - only vertical
 *  Esc                       - reset to start
 */

internal partial class Form : System.Windows.Forms.Form
{
    private readonly Renderer _renderer;

    public Form(Action<Renderer> callback)
    {
        InitializeComponent();
        _renderer = new(PictureBox);
        callback(_renderer);
    }

    private FormWindowState _lastWindowState = FormWindowState.Normal;

    private void Form_Resize(object sender, EventArgs e)
    {
        if (WindowState != _lastWindowState &&
            WindowState != FormWindowState.Minimized)
        {
            _lastWindowState = WindowState;
            _renderer.OnResize();
        }
    }

    private void Form_ResizeEnd(object sender, EventArgs e)
    {
        _renderer.OnResize();
    }

    private bool _isShiftPressed;
    private bool _isLeftMouseButtonPressed;
    private bool _isRightMouseButtonPressed;
    private bool _reduceLeftMouseButtonUp;
    private bool _onlyX;
    private bool _onlyY;

    private bool Smooth => _isShiftPressed || _isRightMouseButtonPressed;

    private void Form_KeyDown(object sender, KeyEventArgs e)
    {
        _isShiftPressed = e.Shift;
        _onlyX = e.Control && !e.Alt;
        _onlyY = e.Alt && !e.Control;

        if (e.Alt)
            e.Handled = true;

        switch (e.KeyCode)
        {
            case Keys.Up:
            case Keys.W:
                _renderer.OnMoveUp(Smooth);
                break;

            case Keys.Down:
            case Keys.S:
                _renderer.OnMoveDown(Smooth);
                break;

            case Keys.Left:
            case Keys.A:
                _renderer.OnMoveLeft(Smooth);
                break;

            case Keys.Right:
            case Keys.D:
                _renderer.OnMoveRight(Smooth);
                break;

            case Keys.Escape:
                _renderer.OnReset();
                break;
        }
    }

    private void Form_KeyUp(object sender, KeyEventArgs e)
    {
        _isShiftPressed = e.Shift;
        _onlyX = e.Control && !e.Alt;
        _onlyY = e.Alt && !e.Control;
    }

    private int _mouseDownX;
    private int _mouseDownY;

    private void PictureBox_MouseDown(object sender, MouseEventArgs e)
    {
        switch (e.Button)
        {
            case MouseButtons.Left:
                _isLeftMouseButtonPressed = true;

                if (!_isRightMouseButtonPressed)
                {
                    _mouseDownX = e.X;
                    _mouseDownY = e.Y;
                }

                break;

            case MouseButtons.Right:
                _isRightMouseButtonPressed = true;

                if (!_isLeftMouseButtonPressed)
                {
                    _mouseDownX = e.X;
                    _mouseDownY = e.Y;
                }

                break;

            case MouseButtons.Middle:

                if (_onlyX)
                    _renderer.OnToggleExpModeX(_mousePosX);
                else
                if (_onlyY)
                    _renderer.OnToggleExpModeY(_mousePosY);
                else
                    _renderer.OnToggleExpMode(_mousePosX, _mousePosY);

                break;
        }
    }

    private void PictureBox_MouseUp(object sender, MouseEventArgs e)
    {
        switch (e.Button)
        {
            case MouseButtons.Left:
                _isLeftMouseButtonPressed = false;

                if (_reduceLeftMouseButtonUp)
                    _reduceLeftMouseButtonUp = false;
                else
                if (_isRightMouseButtonPressed)
                    LeftWithRightMouseButtonsUp(e.X, e.Y);
                else
                {
                    if (_onlyX)
                        _renderer.OnMoveX(e.X - _mouseDownX);
                    else
                    if (_onlyY)
                        _renderer.OnMoveY(e.Y - _mouseDownY);
                    else
                        _renderer.OnMove(e.X - _mouseDownX, e.Y - _mouseDownY);
                }

                break;

            case MouseButtons.Right:
                _isRightMouseButtonPressed = false;

                if (_isLeftMouseButtonPressed)
                {
                    _reduceLeftMouseButtonUp = true;
                    LeftWithRightMouseButtonsUp(e.X, e.Y);
                }

                break;
        }
    }

    private void LeftWithRightMouseButtonsUp(int x, int y)
    {
        var minX = Math.Min(x, _mouseDownX);
        var maxX = Math.Max(x, _mouseDownX);
        var minY = Math.Min(y, _mouseDownY);
        var maxY = Math.Max(y, _mouseDownY);

        if (_onlyX)
            _renderer.OnRangeX(minX, maxX);
        else
        if (_onlyY)
            _renderer.OnRangeY(minY, maxY);
        else
            _renderer.OnRange(minX, maxX, minY, maxY);
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
        {
            if (_onlyX)
                _renderer.OnZoomInX(Smooth, e.X);
            else
            if (_onlyY)
                _renderer.OnZoomInY(Smooth, e.Y);
            else
                _renderer.OnZoomIn(Smooth, e.X, e.Y);
        }
        else if (e.Delta == -120) // Mouse wheel down
        {
            if (_onlyX)
                _renderer.OnZoomOutX(Smooth, e.X);
            else
            if (_onlyY)
                _renderer.OnZoomOutY(Smooth, e.Y);
            else
                _renderer.OnZoomOut(Smooth, e.X, e.Y);
        }
    }
}
