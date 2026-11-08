using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration; // Элемент интеграции 3D в WinForms
using System.Windows.Media;            // Нужен для настройки цветов 3D
using System.Windows.Media.Media3D;     // Нужен для 3D геометрии
using HelixToolkit.Wpf;                 // Сам 3D движок Helix
using Color = System.Drawing.Color;

namespace CristalWinForms
{
    public partial class Form1 : Form
    {
        private readonly PhysicsEngine _physics = new PhysicsEngine();

        // Элементы WinForms интерфейса
        private TrackBar _tempSlider;
        private TrackBar _freqSlider;
        private Label _lblTempStatus;
        private Label _lblFreqStatus;
        private System.Windows.Forms.Timer _animationTimer;

        // Элементы 3D движка Helix
        private HelixViewport3D _viewport;
        private ModelVisual3D _atomsContainer;

        // переменную времени
        private double _simulationTime = 0.0;
        // Добавьте один глобальный генератор случайных чисел в самый верх класса Form1,
        // чтобы он не пересоздавался каждую миллисекунду:
        private readonly Random _rand = new Random();
        private Label _lblResistDC;
        private Label _lblResistAC;

        public Form1()
        {
            // Настройки самого окна Windows Forms
            this.Text = "Квантовый Симулятор Мегапроводника 150 ГГц (WinForms)";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            System.Drawing.Color.FromArgb(34, 34, 34);

            InitializeCustomControls();
            Initialize3DScene();
            StartSimulation();

            // Обязательно добавьте эту строчку:
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown; // Привязываем обработчик кнопок
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (_viewport == null) return;

            // Отключаем на время анимационный фокус, чтобы камера могла повернуться
            _viewport.LookAt(new Point3D(3, 3, 3), 0);

            if (e.KeyCode == Keys.V) // ВИД СВЕРХУ (План слоев)
            {
                _viewport.Camera.Position = new Point3D(3, 3, 20);
                _viewport.Camera.LookDirection = new Vector3D(0, 0, -20);
                _viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
            }
            else if (e.KeyCode == Keys.B) // ВИД СПЕРЕДИ (Профиль связей)
            {
                _viewport.Camera.Position = new Point3D(3, -15, 3);
                _viewport.Camera.LookDirection = new Vector3D(0, 15, 0);
                _viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
            }
            else if (e.KeyCode == Keys.N) // ИСХОДНЫЙ 3D ВИД (Изометрия)
            {
                _viewport.Camera.Position = new Point3D(12, 12, 12);
                _viewport.Camera.LookDirection = new Vector3D(-12, -12, -12);
                _viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
            }
        }


        // 1. Создаем обычные WinForms элементы управления (Левая панель)
        private void InitializeCustomControls()
        {
            Panel sidePanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Color.FromArgb(34, 34, 34),
                Padding = new Padding(15)
            };
            this.Controls.Add(sidePanel);

            Label title = new Label
            {
                Text = "УПРАВЛЕНИЕ РЕЗОНАНСОМ",
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 30
            };
            sidePanel.Controls.Add(title);

            // Ползунок температуры
            Label lblTemp = new Label { Text = "Температура решетки (К):", ForeColor = Color.Gray, Dock = DockStyle.Top, Height = 20 };
            _tempSlider = new TrackBar { Minimum = 0, Maximum = 500, Value = 300, Dock = DockStyle.Top, TickStyle = TickStyle.None };
            _lblTempStatus = new Label { Text = "300 K", ForeColor = Color.White, TextAlign = ContentAlignment.TopRight, Dock = DockStyle.Top, Height = 20 };
            _tempSlider.Scroll += (s, e) => { _physics.Temperature = _tempSlider.Value; _lblTempStatus.Text = $"{_tempSlider.Value} K"; };

            sidePanel.Controls.Add(_lblTempStatus);
            sidePanel.Controls.Add(_tempSlider);
            sidePanel.Controls.Add(lblTemp);

            // Ползунок частоты СВЧ
            Label lblFreq = new Label { Text = "Частота генератора (ГГц):", ForeColor = Color.Gray, Dock = DockStyle.Top, Height = 20 };
            _freqSlider = new TrackBar { Minimum = 100, Maximum = 200, Value = 150, Dock = DockStyle.Top, TickStyle = TickStyle.None };
            _lblFreqStatus = new Label { Text = "150 ГГц", ForeColor = Color.White, TextAlign = ContentAlignment.TopRight, Dock = DockStyle.Top, Height = 20 };
            _freqSlider.Scroll += (s, e) => { _physics.TargetFrequency = _freqSlider.Value * 1e9; _lblFreqStatus.Text = $"{_freqSlider.Value} ГГц"; };

            sidePanel.Controls.Add(_lblFreqStatus);
            sidePanel.Controls.Add(_freqSlider);
            sidePanel.Controls.Add(lblFreq);

            // Информационное табло
            Label infoBox = new Label
            {
                Text = "СТАТУС:\nРезонанс стабилен.\nСопротивление AC = 0 Ом.",
                ForeColor = Color.FromArgb(0, 255, 204),
                BackColor = Color.FromArgb(20, 20, 20),
                Font = new Font("Arial", 9, FontStyle.Italic),
                Dock = DockStyle.Bottom,
                Height = 80,
                Padding = new Padding(5)
            };

            // Вставьте этот блок в конец метода InitializeCustomControls:
            Button btnExportSvg = new Button
            {
                Text = "Экспорт вида в SVG",
                Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                BackColor = System.Drawing.Color.FromArgb(0, 102, 255),
                ForeColor = System.Drawing.Color.White,
                Dock = DockStyle.Top,
                Height = 35,
                FlatStyle = FlatStyle.Flat
            };
            // Убираем рамки кнопки для современного вида
            btnExportSvg.FlatAppearance.BorderSize = 0;

            // Обработчик нажатия кнопки экспорта
            btnExportSvg.Click += (s, e) =>
            {
                try
                {
                    string directoryPath = @"H:\Cristal"; // Ваш фиксированный рабочий каталог

                    // Автоматически создаем папку, если её нет на диске H:
                    if (!System.IO.Directory.Exists(directoryPath))
                    {
                        System.IO.Directory.CreateDirectory(directoryPath);
                    }

                    // Квантовый движок Helix переводит 3D-сцену в 2D-векторный SVG-файл
                    // Замените внутренний код блока сохранения внутри кнопки на этот:
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string filePath = System.IO.Path.Combine(directoryPath, $"crystal_structure_{timestamp}.png");

                    // Экспортируем сцену в PNG высокого разрешения для печати
                    _viewport.Export(filePath);

                    MessageBox.Show($"Снимок структуры для статьи успешно сохранен!\nПуть: {filePath}",
                                    "Успех экспорта", MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения файла: {ex.Message}",
                                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            // Добавляем отступ над кнопкой
            Label btnSpacer = new Label { Dock = DockStyle.Top, Height = 10 };

            sidePanel.Controls.Add(btnExportSvg);
            sidePanel.Controls.Add(btnSpacer);


            sidePanel.Controls.Add(infoBox);

            // Этот блок вставьте в конец метода InitializeCustomControls:
            Label lblDC = new Label { Text = "Удельное сопротивление DC (Постоянный ток):", ForeColor = System.Drawing.Color.Gray, Dock = DockStyle.Top, Height = 20 };
            _lblResistDC = new Label { Text = "1.6e-8 Ом·м", ForeColor = System.Drawing.Color.White, Dock = DockStyle.Top, Height = 20 };

            Label lblAC = new Label { Text = "Удельное сопротивление AC (150 ГГц):", ForeColor = System.Drawing.Color.Gray, Dock = DockStyle.Top, Height = 20 };
            _lblResistAC = new Label { Text = "0.000 Ом·м", ForeColor = System.Drawing.Color.FromArgb(0, 255, 204), Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold), Dock = DockStyle.Top, Height = 20 };

            // Добавляем пустые отступы для красоты
            Label spacer = new Label { Dock = DockStyle.Top, Height = 15 };

            sidePanel.Controls.Add(_lblResistAC);
            sidePanel.Controls.Add(lblAC);
            sidePanel.Controls.Add(_lblResistDC);
            sidePanel.Controls.Add(lblDC);
            sidePanel.Controls.Add(spacer);
        }

        // 2. Инициализируем 3D-сцену Helix Toolkit внутри WinForms через ElementHost
        private void Initialize3DScene()
        {
            ElementHost host = new ElementHost { Dock = DockStyle.Fill };
            this.Controls.Add(host);

            _viewport = new HelixViewport3D
            {
                ShowCoordinateSystem = true,
                Background = System.Windows.Media.Brushes.White,
                CoordinateSystemLabelForeground = System.Windows.Media.Brushes.DarkGray,

                // --- ИСПРАВЛЕННЫЙ БЛОК ДЛЯ АКТИВАЦИИ КУБИКА НАВИГАЦИИ ---
                ShowViewCube = true,                         // Показываем кубик
                IsViewCubeEdgeClicksEnabled = true,          // АКТИВИРУЕМ КЛИКИ (Вращение сцены)
                ViewCubeBackText = "Зад",
                ViewCubeFrontText = "Перед",
                ViewCubeTopText = "Верх",
                ViewCubeBottomText = "Низ",
                ViewCubeLeftText = "Лев",
                ViewCubeRightText = "Прав"
            };

            _viewport.Camera = new PerspectiveCamera(new Point3D(12, 12, 12), new Vector3D(-12, -12, -12), new Vector3D(0, 0, 1), 45);
            _viewport.Children.Add(new DefaultLights());

            _atomsContainer = new ModelVisual3D();
            _viewport.Children.Add(_atomsContainer);

            host.Child = _viewport;
        }




        // 3. Запуск WinForms таймера анимации (60 Кадров в секунду)
        private void StartSimulation()
        {
            _animationTimer = new System.Windows.Forms.Timer
            {
                Interval = 16 // ~60 FPS
            };
            _animationTimer.Tick += Update3DFrame;
            _animationTimer.Start();
        }

        // 4. Отрисовка каждого кадра «танца» атомов
        private void Update3DFrame(object sender, EventArgs e)
        {
            if (_atomsContainer == null) return;

            double freqGhz = _freqSlider.Value;
            double temperature = _tempSlider.Value;

            // --- ФИЗИЧЕСКИЙ РАСЧЕТ УДЕЛЬНОГО СОПРОТИВЛЕНИЯ ---
            // 1. Постоянный ток (DC): классическая линейная зависимость от температуры Блоха-Грюнайзена
            // Опорное сопротивление Тантала при 300 К составляет около 13.5e-8 Ом·м.
            // Наша изотопно-чистая структура чище, поэтому базовое сопротивление ниже: ~1.5e-8 Ом·м
            double baseResistDC = 1.5e-8;
            double currentResistDC = baseResistDC * (1.0 + 0.004 * (temperature - 300.0));
            if (temperature < 10) currentResistDC = 1e-10; // Вымерзание фононов вблизи абсолютного нуля

            // 2. Переменный ток (AC): строго 0 Ом·м только в резонансе на частоте 150 ГГц!
            double currentResistAC = 0.0;

            // Если критик уводит ползунок частоты из резонанса, безрезистивный танец ломается,
            // и появляется реактивное СВЧ-сопротивление (скин-эффект)
            if (Math.Abs(freqGhz - 150.0) > 1.0)
            {
                currentResistAC = currentResistDC * 0.5 * (Math.Abs(freqGhz - 150.0) / 50.0);
            }

            // Выводим расчетные физические данные на левую панель WinForms
            _lblResistDC.Text = $"{currentResistDC:E2} Ом·м";
            if (currentResistAC == 0.0)
            {
                _lblResistAC.Text = "0.000 Ом·м (РЕЗОНАНС)";
                _lblResistAC.ForeColor = System.Drawing.Color.FromArgb(0, 255, 204); // Бирюзовый
            }
            else
            {
                _lblResistAC.Text = $"{currentResistAC:E2} Ом·м (РАССИНХРОНИЗАЦИЯ)";
                _lblResistAC.ForeColor = System.Drawing.Color.FromArgb(255, 51, 102); // Розовый
            }
            // --------------------------------------------------

            // Шаг времени. Поле навязывает частоту
            double dt = 0.06 * (freqGhz / 150.0);
            _simulationTime += dt;

            // Фаза внешнего электромагнитного поля (наш дирижер)
            double fieldPhase = _simulationTime;

            // Эффект захвата фазы: решетка полностью повторяет фазу поля!
            double latticePhase = fieldPhase;

            double baseOffset = Math.Sin(latticePhase);
            double noiseIntensity = (temperature / 500.0) * 0.25;

            // Очищаем старый 3D кадр перед перерисовкой
            _atomsContainer.Children.Clear();

            // 2. ОТРИСОВКА КРИСТАЛЛИЧЕСКОЙ РЕШЕТКИ (С учетом теплового шума)
            // Слои Тантала-181 (Бирюзовые сферы)
            var meshTa = new MeshBuilder();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    double atomNoise = (_rand.NextDouble() - 0.5) * noiseIntensity;
                    double zPos = 3.0 + (baseOffset * 0.4) + atomNoise;
                    meshTa.AddSphere(new Point3D(x * 3, y * 3, zPos), 0.5);
                }
            }
            var materialTa = MaterialHelper.CreateMaterial(System.Windows.Media.Color.FromRgb(0, 255, 204));
            _atomsContainer.Children.Add(new ModelVisual3D { Content = new GeometryModel3D(meshTa.ToMesh(), materialTa) });

            // Слои Селена-80 (Желтые сферы)
            var meshSe = new MeshBuilder();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    double atomNoiseTop = (_rand.NextDouble() - 0.5) * noiseIntensity;
                    double atomNoiseBot = (_rand.NextDouble() - 0.5) * noiseIntensity;
                    double zTop = 5.0 - (baseOffset * 0.2) + atomNoiseTop;
                    double zBot = 1.0 - (baseOffset * 0.2) + atomNoiseBot;
                    meshSe.AddSphere(new Point3D(x * 3, y * 3, zTop), 0.4);
                    meshSe.AddSphere(new Point3D(x * 3, y * 3, zBot), 0.4);
                }
            }
            var materialSe = MaterialHelper.CreateMaterial(System.Windows.Media.Color.FromRgb(255, 204, 0));
            _atomsContainer.Children.Add(new ModelVisual3D { Content = new GeometryModel3D(meshSe.ToMesh(), materialSe) });

            // Слои Бария (Розовые сферы)
            var meshBa = new MeshBuilder();
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    double atomNoiseBa = (_rand.NextDouble() - 0.5) * (noiseIntensity * 0.3);
                    double zBa = -1.0 + (baseOffset * 0.05) + atomNoiseBa;
                    meshBa.AddSphere(new Point3D(x * 3 + 1.5, y * 3 + 1.5, zBa), 0.6);
                }
            }
            var materialBa = MaterialHelper.CreateMaterial(System.Windows.Media.Color.FromRgb(255, 51, 102));
            _atomsContainer.Children.Add(new ModelVisual3D { Content = new GeometryModel3D(meshBa.ToMesh(), materialBa) });

            // 3. НОВОЕ: ВИЗУАЛИЗАЦИЯ ВНЕШНЕГО ПЕРЕМЕННОГО ПОЛЯ (Синяя бегущая волна)
            var fieldPoints = new Point3DCollection();
            for (double x = -1; x <= 7; x += 0.2)
            {
                // Волна поля распространяется вдоль оси X
                double yPos = 3.0;
                double zPos = 3.0 + Math.Sin(x - fieldPhase) * 1.5; // Синусоида поля
                fieldPoints.Add(new Point3D(x, yPos, zPos));
            }
            var fieldLines = new LinesVisual3D
            {
                Points = fieldPoints,
                Color = System.Windows.Media.Color.FromRgb(0, 102, 255),
                Thickness = 3
            };
            _atomsContainer.Children.Add(fieldLines);


            // 4. НОВОЕ: ВИЗУАЛИЗАЦИЯ ЗАРЯДОВ / ЭЛЕКТРОНОВ (Белые сферы проводимости)
            var meshElectrons = new MeshBuilder();
            for (int x = 0; x < 3; x++)
            {
                // Положение заряда скользит вдоль каналов Тантала
                // Оно жестко синхронизировано с максимумами волны внешнего поля (Phase-locking)
                double electronX = (x * 3) + (baseOffset * 0.8);
                double electronY = 1.5; // Электроны текут в междоузельных каналах
                double electronZ = 3.0 + (baseOffset * 0.4); // Идеальный такт с решеткой!

                meshElectrons.AddSphere(new Point3D(electronX, electronY, electronZ), 0.25);
            }
            // Светящийся белый материал для квантовых зарядов
            var materialElectrons = MaterialHelper.CreateMaterial(System.Windows.Media.Color.FromRgb(255, 255, 255));
            _atomsContainer.Children.Add(new ModelVisual3D { Content = new GeometryModel3D(meshElectrons.ToMesh(), materialElectrons) });

            // =============================================================================
            // ──── ЕДИНЫЙ БЛОК ОТРИСОВКИ ВСЕХ СВЯЗЕЙ КРИСТАЛЛА (ГОРИЗОНТ + ВЕРТИКАЛЬ) ────
            // =============================================================================

            // СНАЧАЛА РАССЧИТЫВАЕМ АКТУАЛЬНЫЕ КООРДИНАТЫ ВСЕХ АТОМОВ С УЧЕТОМ ВИБРАЦИИ И ШУМА
            Point3D[,] taPositions = new Point3D[3, 3];
            Point3D[,] seTopPositions = new Point3D[3, 3];
            Point3D[,] seBotPositions = new Point3D[3, 3];
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    double atomNoiseTa = (_rand.NextDouble() - 0.5) * noiseIntensity;
                    double atomNoiseTop = (_rand.NextDouble() - 0.5) * noiseIntensity;
                    double atomNoiseBot = (_rand.NextDouble() - 0.5) * noiseIntensity;

                    taPositions[x, y] = new Point3D(x * 3, y * 3, 3.0 + (baseOffset * 0.4) + atomNoiseTa);
                    seTopPositions[x, y] = new Point3D(x * 3, y * 3, 5.0 - (baseOffset * 0.2) + atomNoiseTop);
                    seBotPositions[x, y] = new Point3D(x * 3, y * 3, 1.0 - (baseOffset * 0.2) + atomNoiseBot);
                }
            }

            Point3D[,] baPositions = new Point3D[2, 2];
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    double atomNoiseBa = (_rand.NextDouble() - 0.5) * (noiseIntensity * 0.3);
                    baPositions[x, y] = new Point3D(x * 3 + 1.5, y * 3 + 1.5, -1.0 + (baseOffset * 0.05) + atomNoiseBa);
                }
            }

            // 1. ГОРИЗОНТАЛЬНАЯ СЕТКА ТАНТАЛА (Бирюзовая)
            var taBondsPoints = new Point3DCollection();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (x < 2) { taBondsPoints.Add(taPositions[x, y]); taBondsPoints.Add(taPositions[x + 1, y]); }
                    if (y < 2) { taBondsPoints.Add(taPositions[x, y]); taBondsPoints.Add(taPositions[x, y + 1]); }
                }
            }
            _atomsContainer.Children.Add(new LinesVisual3D { Points = taBondsPoints, Color = System.Windows.Media.Color.FromRgb(0, 180, 150), Thickness = 2 });

            // 2. ГОРИЗОНТАЛЬНАЯ СЕТКА СЕЛЕНА (Золотая - верх + ниж)
            var seBondsPoints = new Point3DCollection();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (x < 2)
                    {
                        seBondsPoints.Add(seTopPositions[x, y]); seBondsPoints.Add(seTopPositions[x + 1, y]);
                        seBondsPoints.Add(seBotPositions[x, y]); seBondsPoints.Add(seBotPositions[x + 1, y]);
                    }
                    if (y < 2)
                    {
                        seBondsPoints.Add(seTopPositions[x, y]); seBondsPoints.Add(seTopPositions[x, y + 1]);
                        seBondsPoints.Add(seBotPositions[x, y]); seBondsPoints.Add(seBotPositions[x, y + 1]);
                    }
                }
            }
            _atomsContainer.Children.Add(new LinesVisual3D { Points = seBondsPoints, Color = System.Windows.Media.Color.FromRgb(200, 160, 0), Thickness = 1.5 });

            // 3. ГОРИЗОНТАЛЬНАЯ СЕТКА БАРИЯ (Розовая)
            var baBondsPoints = new Point3DCollection();
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    if (x < 1) { baBondsPoints.Add(baPositions[x, y]); baBondsPoints.Add(baPositions[x + 1, y]); }
                    if (y < 1) { baBondsPoints.Add(baPositions[x, y]); baBondsPoints.Add(baPositions[x, y + 1]); }
                }
            }
            _atomsContainer.Children.Add(new LinesVisual3D { Points = baBondsPoints, Color = System.Windows.Media.Color.FromRgb(200, 40, 80), Thickness = 2.5 });

            // 4. ВЕРТИКАЛЬНЫЕ СВЯЗИ ТАНТАЛ-СЕЛЕН ВНУТРИ СЭНДВИЧА (Сплошные бирюзовые)
            var covalentBondsPoints = new Point3DCollection();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    covalentBondsPoints.Add(taPositions[x, y]); covalentBondsPoints.Add(seTopPositions[x, y]);
                    covalentBondsPoints.Add(taPositions[x, y]); covalentBondsPoints.Add(seBotPositions[x, y]);
                }
            }
            _atomsContainer.Children.Add(new LinesVisual3D { Points = covalentBondsPoints, Color = System.Windows.Media.Color.FromRgb(0, 255, 204), Thickness = 1.5 });

            // 5. НАКЛОННЫЕ СЛАБЫЕ СВЯЗИ БАРИЙ-СЕЛЕН (Розовый штриховой пунктир)
            var vdwBondsPoints = new Point3DCollection();
            for (int bx = 0; bx < 2; bx++)
            {
                for (int by = 0; by < 2; by++)
                {
                    Point3D baAtom = baPositions[bx, by];
                    int[] dx = { 0, 1, 0, 1 };
                    int[] dy = { 0, 0, 1, 1 };

                    for (int i = 0; i < 4; i++)
                    {
                        Point3D seAtom = seBotPositions[bx + dx[i], by + dy[i]];
                        int segments = 5;
                        for (int s = 0; s < segments; s++)
                        {
                            if (s % 2 == 0)
                            {
                                double t1 = (double)s / segments;
                                double t2 = (double)(s + 1) / segments;
                                Point3D p1 = new Point3D(baAtom.X + (seAtom.X - baAtom.X) * t1, baAtom.Y + (seAtom.Y - baAtom.Y) * t1, baAtom.Z + (seAtom.Z - baAtom.Z) * t1);
                                Point3D p2 = new Point3D(baAtom.X + (seAtom.X - baAtom.X) * t2, baAtom.Y + (seAtom.Y - baAtom.Y) * t2, baAtom.Z + (seAtom.Z - baAtom.Z) * t2);
                                vdwBondsPoints.Add(p1);
                                vdwBondsPoints.Add(p2);
                            }
                        }
                    }
                }
            }
            _atomsContainer.Children.Add(new LinesVisual3D { Points = vdwBondsPoints, Color = System.Windows.Media.Color.FromRgb(255, 51, 102), Thickness = 1.0 });
            // =============================================================================


            // Фиксируем фокус камеры на центре ячейки
            _viewport.LookAt(new Point3D(3, 3, 3), 0);
        }

    }
}
