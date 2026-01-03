using System.Diagnostics;

namespace CornwallRoom;

public sealed class MainForm : Form
{
    private readonly PictureBox _picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    private readonly Panel _rightPanel = new() { Dock = DockStyle.Right, Width = 380, Padding = new Padding(12) };
    private readonly Button _btnRender = new() { Text = "Рендеринг", Dock = DockStyle.Top, Height = 40 };
    private readonly Label _lblStatus = new() { Text = "Готово", Dock = DockStyle.Top, AutoSize = false, Height = 44 };

    private readonly CheckBox _chkMirrorSphere = new() { Text = "Зеркальность сфер", Dock = DockStyle.Top, Checked = false };
    private readonly CheckBox _chkMirrorCube = new() { Text = "Зеркальность кубов", Dock = DockStyle.Top, Checked = false };
    private readonly CheckBox _chkGlassSphere = new() { Text = "Прозрачность сфер", Dock = DockStyle.Top, Checked = false };
    private readonly CheckBox _chkGlassCube = new() { Text = "Прозрачность кубов", Dock = DockStyle.Top, Checked = false };

    private readonly ComboBox _cmbMirrorWall = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbSecondLightPlace = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly CancellationTokenSource _cts = new();
    private Bitmap? _last;

    public MainForm()
    {
        Text = "Корнуэльская комната";
        StartPosition = FormStartPosition.CenterScreen;

        // Основная компоновка: изображение + правая панель с элементами управления
        Controls.Add(_picture);
        Controls.Add(_rightPanel);

        _rightPanel.Controls.Add(_lblStatus);
        _rightPanel.Controls.Add(_btnRender);

        _rightPanel.Controls.Add(Spacer());

        _rightPanel.Controls.Add(new Label
        {
            Text = "• Глубина рекурсии: 5",
            Dock = DockStyle.Top,
            Height = 18
        });

        _rightPanel.Controls.Add(Spacer());

        _rightPanel.Controls.Add(new Label
        {
            Text = "• Сэмплов на пиксель: 5",
            Dock = DockStyle.Top,
            Height = 18
        });

        _rightPanel.Controls.Add(Spacer());

        _rightPanel.Controls.Add(new Label
        {
            Text = "Фиксированные параметры:",
            Dock = DockStyle.Top,
            Height = 18
        });

        _rightPanel.Controls.Add(Spacer());

        // Материалы объектов: можно делать сферы и кубы зеркальными или прозрачными
        _rightPanel.Controls.Add(_chkGlassCube);
        _rightPanel.Controls.Add(_chkGlassSphere);
        _rightPanel.Controls.Add(_chkMirrorCube);
        _rightPanel.Controls.Add(_chkMirrorSphere);
        _rightPanel.Controls.Add(new Label { Text = "Материалы объектов:", Dock = DockStyle.Top, Height = 18 });

        _rightPanel.Controls.Add(Spacer());

        // Выбор зеркальной стены
        _rightPanel.Controls.Add(_cmbMirrorWall);
        _rightPanel.Controls.Add(new Label { Text = "Зеркальная стена:", Dock = DockStyle.Top, Height = 18 });

        // Второй источник света: можно разместить на разных стенах
        _cmbSecondLightPlace.Items.AddRange(new object[] { "Нет", "Правая", "Левая", "Пол", "Задняя", "Передняя" });
        _cmbSecondLightPlace.SelectedIndex = 0;
        _rightPanel.Controls.Add(_cmbSecondLightPlace);
        _rightPanel.Controls.Add(new Label { Text = "2-й источник света:", Dock = DockStyle.Top, Height = 18 });

        _rightPanel.Controls.Add(Spacer());

        // Инициализация выбора зеркальной стены
        _cmbMirrorWall.Items.AddRange(new object[] { "Нет", "Левая", "Правая", "Пол", "Потолок", "Задняя", "Передняя" });
        _cmbMirrorWall.SelectedIndex = 0;

        // Обработчики событий
        _btnRender.Click += async (_, _) => await RenderAsync();
        FormClosing += (_, _) => _cts.Cancel();

        SetFormSize();
        Shown += async (_, _) => await RenderAsync(); // Автоматический рендеринг при запуске
    }

    /// <summary>
    /// Устанавливает размер формы в соответствии с размером изображения и панели управления.
    /// </summary>
    private void SetFormSize()
    {
        int imageWidth = 1024;
        int imageHeight = 768;
        int panelWidth = 380;

        int desiredClientWidth = imageWidth + panelWidth;
        int desiredClientHeight = imageHeight;

        ClientSize = new Size(desiredClientWidth, desiredClientHeight);

        // Фиксированный размер формы для сохранения правильной компоновки
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
    }

    private static Control Spacer() => new Panel { Dock = DockStyle.Top, Height = 10 };

    /// <summary>
    /// Основной метод асинхронного рендеринга
    /// </summary>
    private async Task RenderAsync()
    {
        _btnRender.Enabled = false;
        _lblStatus.Text = "Выполняется рендер...";

        try
        {
            int w = 1024;
            int h = 768;

            // Сбор всех параметров рендеринга из интерфейса
            var opts = new RenderOptions
            {
                Width = w,
                Height = h,
                MirrorSpheres = _chkMirrorSphere.Checked,
                MirrorCubes = _chkMirrorCube.Checked,
                TransparentSpheres = _chkGlassSphere.Checked,
                TransparentCubes = _chkGlassCube.Checked,
                MirrorWall = (MirrorWall)_cmbMirrorWall.SelectedIndex,
                SecondLightPlacement = (SecondLightPlacement)_cmbSecondLightPlace.SelectedIndex
            };

            // Асинхронный запуск трассировки лучей с измерением времени
            var sw = Stopwatch.StartNew();
            var bmp = await Task.Run(() => RayTracer.RenderCornwall(opts, _cts.Token));
            sw.Stop();

            // Обновление изображения и статуса
            _last?.Dispose();
            _last = bmp;
            _picture.Image = bmp;
            _lblStatus.Text = $"Готово. Время: {sw.ElapsedMilliseconds} мс";
        }
        catch (OperationCanceledException) { _lblStatus.Text = "Отменено."; }
        catch (Exception ex) { _lblStatus.Text = "Ошибка: " + ex.Message; }
        finally { _btnRender.Enabled = true; }
    }
}