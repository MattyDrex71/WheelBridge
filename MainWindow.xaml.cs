
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using Vortice.DirectInput;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace WheelBridge
{
    public partial class MainWindow : Window
    {
        private IDirectInput8? _directInput;
        private IDirectInputDevice8? _joystick;
        private ViGEmClient? _vigemClient;
        private IXbox360Controller? _virtualController;
        private DispatcherTimer? _pollTimer;
        private bool[]? _previousButtons;
        private string? _captureTarget;
        private string? _captureAxisTarget;
        private Dictionary<string, double> _axisCaptureBaseline = new();
        private Dictionary<string, double> _lastAxisValues = new();
        private readonly string _appFolder;
        private readonly string _profilesFolder;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        private static readonly string[] AxisNames =
        {
            "X", "Y", "Z", "Rotation X",
            "Rotation Y", "Rotation Z",
            "Slider 1", "Slider 2"
        };

        public sealed class ProfileListEntry
        {
            public string Name { get; set; } = "";
            public string FilePath { get; set; } = "";
            public override string ToString() => Name;
        }

        public sealed class WheelProfile
        {
            public string Name { get; set; } = "Il mio profilo";
            public string SteeringAxis { get; set; } = "X";
            public string ThrottleAxis { get; set; } = "Y";
            public string BrakeAxis { get; set; } = "Z";

            public bool InvertSteering { get; set; }
            public bool InvertThrottle { get; set; }
            public bool InvertBrake { get; set; }

            public int SensitivityPercent { get; set; } = 400;
            public int DeadzonePercent { get; set; } = 5;
            public bool ShowRawValues { get; set; } = true;

            public int ButtonA { get; set; }
            public int ButtonB { get; set; }
            public int ButtonX { get; set; }
            public int ButtonY { get; set; }
            public int ButtonLB { get; set; }
            public int ButtonRB { get; set; }
            public int ButtonStart { get; set; }
            public int ButtonBack { get; set; }

            public int ButtonL3 { get; set; }
            public int ButtonR3 { get; set; }
            public int ButtonDPadUp { get; set; }
            public int ButtonDPadDown { get; set; }
            public int ButtonDPadLeft { get; set; }
            public int ButtonDPadRight { get; set; }
        }

        private sealed class AppSettings
        {
            public int SensitivityPercent { get; set; } = 400;
            public int DeadzonePercent { get; set; } = 5;
            public bool ShowRawValues { get; set; } = true;
        }

        public MainWindow()
        {
            InitializeComponent();

            _appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WheelBridge");

            _profilesFolder = Path.Combine(_appFolder, "Profiles");
            Directory.CreateDirectory(_profilesFolder);

            InitializeMappingControls();
            LoadSettings();
            RefreshProfiles();
            SetActiveNavigation(DashboardNavButton);

            UpdateGamepadVisuals(Array.Empty<bool>());
        }

        private void InitializeVirtualController()
        {
            try
            {
                _vigemClient = new ViGEmClient();
                _virtualController = _vigemClient.CreateXbox360Controller();

                _virtualController.AutoSubmitReport = false;
                _virtualController.Connect();

                DashboardInputStatus.Text =
                    "Controller Xbox 360 virtuale collegato.";
            }
            catch (Exception ex)
            {
                _virtualController = null;

                _vigemClient?.Dispose();
                _vigemClient = null;

                DashboardInputStatus.Text =
                    "Errore controller virtuale: " + ex.Message;
            }
        }

        private void InitializeMappingControls()
        {
            foreach (var combo in new[]
            {
                SteeringAxisCombo,
                ThrottleAxisCombo,
                BrakeAxisCombo
            })
            {
                combo.ItemsSource = AxisNames;
            }

            SteeringAxisCombo.SelectedItem = "X";
            ThrottleAxisCombo.SelectedItem = "Y";
            BrakeAxisCombo.SelectedItem = "Z";


var buttonChoices = new List<string> { "Non assegnato" };

buttonChoices.AddRange(
    Enumerable.Range(1, 128).Select(n => $"Pulsante {n}"));

for (int pov = 1; pov <= 4; pov++)
{
    buttonChoices.Add($"POV {pov} - Su");
    buttonChoices.Add($"POV {pov} - Giù");
    buttonChoices.Add($"POV {pov} - Sinistra");
    buttonChoices.Add($"POV {pov} - Destra");
}


            foreach (var combo in GetButtonCombos())
            {
                combo.ItemsSource = buttonChoices;
                combo.SelectedIndex = 0;
            }
        }

private static bool IsPovDirectionPressed(int povValue, int direction)
{
    // DirectInput usa -1 quando il POV è al centro.
    if (povValue < 0)
        return false;

    int angle = povValue % 36000;

    return direction switch
    {
        0 => angle >= 31500 || angle <= 4500, // Su
        1 => angle >= 13500 && angle <= 22500, // Giù
        2 => angle >= 22500 && angle <= 31500, // Sinistra
        3 => angle >= 4500 && angle <= 13500,  // Destra
        _ => false
    };
}

private static bool[] GetCombinedButtonStates(JoystickState state)
{
    // 128 pulsanti normali + 16 direzioni POV (4 POV x 4 direzioni).
    bool[] result = new bool[144];

    Array.Copy(
        state.Buttons,
        result,
        Math.Min(state.Buttons.Length, 128));

    for (int pov = 0; pov < state.PointOfViewControllers.Length; pov++)
    {
        int value = state.PointOfViewControllers[pov];

        for (int direction = 0; direction < 4; direction++)
        {
            result[128 + pov * 4 + direction] =
                IsPovDirectionPressed(value, direction);
        }
    }

    return result;
}

private static string GetButtonDescription(int index)
{
    if (index < 128)
        return $"pulsante fisico {index + 1}";

    int pov = (index - 128) / 4 + 1;
    int direction = (index - 128) % 4;

    string directionName = direction switch
    {
        0 => "Su",
        1 => "Giù",
        2 => "Sinistra",
        3 => "Destra",
        _ => "?"
    };

    return $"POV {pov} - {directionName}";
}

        private ComboBox[] GetButtonCombos() => new[]
        {
            ButtonACombo, ButtonBCombo, ButtonXCombo, ButtonYCombo,
            ButtonLBCombo, ButtonRBCombo, ButtonStartCombo, ButtonBackCombo,
            ButtonL3Combo, ButtonR3Combo,
            ButtonDPadUpCombo, ButtonDPadDownCombo,
            ButtonDPadLeftCombo, ButtonDPadRightCombo
        };

        // =========================================================
        // ANTEPRIMA CONTROLLER XBOX
        // =========================================================

        private void UpdateGamepadVisuals(bool[] buttons)
        {
            UpdateVisualButton("A", VisualA, ButtonACombo, buttons);
            UpdateVisualButton("B", VisualB, ButtonBCombo, buttons);
            UpdateVisualButton("X", VisualX, ButtonXCombo, buttons);
            UpdateVisualButton("Y", VisualY, ButtonYCombo, buttons);

            UpdateVisualButton("LB", VisualLB, ButtonLBCombo, buttons);
            UpdateVisualButton("RB", VisualRB, ButtonRBCombo, buttons);

            UpdateVisualButton("Back", VisualBack, ButtonBackCombo, buttons);
            UpdateVisualButton("Start", VisualStart, ButtonStartCombo, buttons);

            UpdateVisualButton("L3", VisualL3, ButtonL3Combo, buttons);
            UpdateVisualButton("R3", VisualR3, ButtonR3Combo, buttons);

            UpdateVisualButton("DPadUp", VisualDPadUp, ButtonDPadUpCombo, buttons);
            UpdateVisualButton("DPadDown", VisualDPadDown, ButtonDPadDownCombo, buttons);
            UpdateVisualButton("DPadLeft", VisualDPadLeft, ButtonDPadLeftCombo, buttons);
            UpdateVisualButton("DPadRight", VisualDPadRight, ButtonDPadRightCombo, buttons);

            if (GamepadSelectionText == null)
                return;

            if (_captureTarget != null)
            {
                GamepadSelectionText.Text =
                    $"RILEVA ATTIVO: premi il pulsante fisico da associare a {_captureTarget}.";
            }
            else
            {
                var pressedNames = GetPressedMappedNames(buttons);

                GamepadSelectionText.Text = pressedNames.Count > 0
                    ? "Pulsanti premuti: " + string.Join(", ", pressedNames)
                    : "Controller pronto. Premi un pulsante del volante per verificarlo.";
            }
        }

        private void UpdateVisualButton(
            string target,
            Border visual,
            ComboBox combo,
            bool[] buttons)
        {
            int physicalIndex = combo.SelectedIndex - 1;

            bool isPressed =
                physicalIndex >= 0 &&
                physicalIndex < buttons.Length &&
                buttons[physicalIndex];

            bool isCapturing =
                string.Equals(_captureTarget, target,
                    StringComparison.OrdinalIgnoreCase);

            if (isPressed)
            {
                visual.Background = new SolidColorBrush(Color.FromRgb(24, 115, 77));
                visual.BorderBrush = new SolidColorBrush(Color.FromRgb(75, 235, 157));
                visual.BorderThickness = new Thickness(3);
            }
            else if (isCapturing)
            {
                visual.Background = new SolidColorBrush(Color.FromRgb(35, 91, 165));
                visual.BorderBrush = new SolidColorBrush(Color.FromRgb(105, 174, 255));
                visual.BorderThickness = new Thickness(3);
            }
            else
            {
                visual.Background = new SolidColorBrush(Color.FromRgb(52, 75, 102));
                visual.BorderBrush = new SolidColorBrush(Color.FromRgb(113, 137, 165));
                visual.BorderThickness = new Thickness(2);
            }
        }

        private List<string> GetPressedMappedNames(bool[] buttons)
        {
            var result = new List<string>();

            AddPressedName("A", ButtonACombo, buttons, result);
            AddPressedName("B", ButtonBCombo, buttons, result);
            AddPressedName("X", ButtonXCombo, buttons, result);
            AddPressedName("Y", ButtonYCombo, buttons, result);
            AddPressedName("LB", ButtonLBCombo, buttons, result);
            AddPressedName("RB", ButtonRBCombo, buttons, result);
            AddPressedName("START", ButtonStartCombo, buttons, result);
            AddPressedName("BACK", ButtonBackCombo, buttons, result);
            AddPressedName("L3", ButtonL3Combo, buttons, result);
            AddPressedName("R3", ButtonR3Combo, buttons, result);
            AddPressedName("DPAD SU", ButtonDPadUpCombo, buttons, result);
            AddPressedName("DPAD GIÙ", ButtonDPadDownCombo, buttons, result);
            AddPressedName("DPAD SINISTRA", ButtonDPadLeftCombo, buttons, result);
            AddPressedName("DPAD DESTRA", ButtonDPadRightCombo, buttons, result);

            return result;
        }

        private static void AddPressedName(
            string name,
            ComboBox combo,
            bool[] buttons,
            List<string> result)
        {
            int physicalIndex = combo.SelectedIndex - 1;

            if (physicalIndex >= 0 &&
                physicalIndex < buttons.Length &&
                buttons[physicalIndex])
            {
                result.Add(name);
            }
        }

        // =========================================================
        // AVVIO E NAVIGAZIONE
        // =========================================================

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _directInput = DInput.DirectInput8Create();
                InitializeVirtualController();
                RefreshDevices();
            }
            catch (Exception ex)
            {
                DeviceStatusText.Text =
                    "Errore durante l'avvio: " + ex.Message;
            }
        }

        private void SetActiveNavigation(Button active)
        {
            var buttons = new[]
            {
                DashboardNavButton, DevicesNavButton, MappingNavButton,
                ProfilesNavButton, SettingsNavButton
            };

            foreach (var button in buttons)
            {
                bool selected = ReferenceEquals(button, active);

                button.Background = selected
                    ? new SolidColorBrush(Color.FromRgb(28, 49, 76))
                    : Brushes.Transparent;

                button.Foreground = selected
                    ? new SolidColorBrush(Color.FromRgb(225, 239, 255))
                    : new SolidColorBrush(Color.FromRgb(165, 180, 200));

                button.BorderBrush = selected
                    ? new SolidColorBrush(Color.FromRgb(61, 132, 242))
                    : Brushes.Transparent;
            }
        }

        private void ShowPage(FrameworkElement page, Button active)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            DevicesPage.Visibility = Visibility.Collapsed;
            MappingPage.Visibility = Visibility.Collapsed;
            ProfilesPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;

            page.Visibility = Visibility.Visible;
            SetActiveNavigation(active);
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
            => ShowPage(DashboardPage, DashboardNavButton);

        private void Devices_Click(object sender, RoutedEventArgs e)
            => ShowPage(DevicesPage, DevicesNavButton);

        private void Mapping_Click(object sender, RoutedEventArgs e)
            => ShowPage(MappingPage, MappingNavButton);

        private void Profiles_Click(object sender, RoutedEventArgs e)
            => ShowPage(ProfilesPage, ProfilesNavButton);

        private void Settings_Click(object sender, RoutedEventArgs e)
            => ShowPage(SettingsPage, SettingsNavButton);

        // =========================================================
        // DISPOSITIVI
        // =========================================================

        private void RefreshDevices()
        {
            DeviceComboBox.Items.Clear();

            try
            {
                _directInput ??= DInput.DirectInput8Create();

                var devices = _directInput.GetDevices(
                    DeviceClass.GameControl,
                    DeviceEnumerationFlags.AttachedOnly);

                foreach (var device in devices)
                {
                    string name = string.IsNullOrWhiteSpace(device.ProductName)
                        ? "Dispositivo di gioco"
                        : device.ProductName;

                    DeviceComboBox.Items.Add(
                        new DeviceChoice(name, device.InstanceGuid));
                }

                if (DeviceComboBox.Items.Count > 0)
                {
                    DeviceComboBox.SelectedIndex = 0;
                    DeviceStatusText.Text =
                        $"Dispositivi trovati: {DeviceComboBox.Items.Count}. Seleziona il volante e premi COLLEGA.";
                }
                else
                {
                    DeviceStatusText.Text =
                        "Nessun dispositivo di gioco rilevato. Controlla il collegamento USB.";
                }
            }
            catch (Exception ex)
            {
                DeviceStatusText.Text =
                    "Errore durante la ricerca: " + ex.Message;
            }
        }

        private sealed class DeviceChoice
        {
            public string Name { get; }
            public Guid Id { get; }

            public DeviceChoice(string name, Guid id)
            {
                Name = name;
                Id = id;
            }
        }

        private void RefreshDevices_Click(object sender, RoutedEventArgs e)
            => RefreshDevices();

        private void ConnectDevice_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem is not DeviceChoice selected)
            {
                DeviceStatusText.Text = "Seleziona prima un dispositivo.";
                return;
            }

            DisconnectJoystick();

            IDirectInputDevice8? newDevice = null;

            try
            {
                newDevice = _directInput!.CreateDevice(selected.Id);

                var windowHandle = new WindowInteropHelper(this).Handle;

                newDevice.SetCooperativeLevel(
                    windowHandle,
                    CooperativeLevel.NonExclusive |
                    CooperativeLevel.Background);

                var formatResult = newDevice.SetDataFormat<RawJoystickState>();

                if (formatResult.Failure)
                    throw new InvalidOperationException(
                        "Impossibile configurare il formato degli assi.");

                var acquireResult = newDevice.Acquire();

                if (acquireResult.Failure)
                    throw new InvalidOperationException(
                        "Windows non ha consentito di acquisire il volante. Chiudi il pannello del volante e riprova.");

                _joystick = newDevice;
                newDevice = null;
                _previousButtons = null;

                _pollTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(30)
                };

                _pollTimer.Tick += PollTimer_Tick;
                _pollTimer.Start();

                DeviceStatusText.Text = "COLLEGATO: " + selected.Name;
                DashboardDeviceStatus.Text = "Collegato · " + selected.Name;

                DashboardDeviceStatus.Foreground =
                    new SolidColorBrush(Color.FromRgb(99, 216, 160));

                DashboardInputStatus.Text =
                    "Lettura DirectInput attiva. Muovi il volante e i pedali per vedere i valori.";

                PollJoystick();
            }
            catch (Exception ex)
            {
                newDevice?.Dispose();
                DeviceStatusText.Text = "Errore di collegamento: " + ex.Message;
            }
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
            => PollJoystick();

        // =========================================================
        // LETTURA DEL VOLANTE
        // =========================================================

        private void PollJoystick()
        {
            if (_joystick == null)
                return;

            try
            {
                var pollResult = _joystick.Poll();

                if (pollResult.Failure)
                {
                    var acquireResult = _joystick.Acquire();

                    if (acquireResult.Failure)
                    {
                        DeviceStatusText.Text =
                            "In attesa di riacquisire il volante...";
                        return;
                    }

                    pollResult = _joystick.Poll();
                    if (pollResult.Failure)
                        return;
                }

                var state = _joystick.GetCurrentJoystickState();
                var currentAxes = GetAxisValues(state);

                ProcessAxisCapture(currentAxes);
                _lastAxisValues = currentAxes;

                AxisXText.Text = $"Asse X: {state.X}";
                AxisYText.Text = $"Asse Y: {state.Y}";
                AxisZText.Text = $"Asse Z: {state.Z}";
                AxisRXText.Text = $"Rotazione X: {state.RotationX}";
                AxisRYText.Text = $"Rotazione Y: {state.RotationY}";
                AxisRZText.Text = $"Rotazione Z: {state.RotationZ}";

                Slider0Text.Text = $"Slider 1: {state.Sliders[0]}";
                Slider1Text.Text = $"Slider 2: {state.Sliders[1]}";

var pressedButtons = Enumerable
    .Range(0, state.Buttons.Length)
    .Where(i => state.Buttons[i])
    .Select(i => $"B{i + 1}")
    .ToList();

var povStates = GetCombinedButtonStates(state);

for (int i = 128; i < povStates.Length; i++)
{
    if (povStates[i])
        pressedButtons.Add(GetButtonDescription(i));
}
ButtonsText.Text = pressedButtons.Count == 0
    ? "Nessun pulsante premuto"
    : string.Join("   ·   ", pressedButtons);

var allButtonStates = GetCombinedButtonStates(state);

ProcessButtonCapture(allButtonStates);
UpdateGamepadVisuals(allButtonStates);
                UpdateMappedMonitor(state);
                UpdateVirtualController(state);

                if (!ShowRawValuesCheck.IsChecked.GetValueOrDefault())
                {
                    AxisXText.Text = "Asse X: nascosto";
                    AxisYText.Text = "Asse Y: nascosto";
                    AxisZText.Text = "Asse Z: nascosto";
                    AxisRXText.Text = "Rotazione X: nascosta";
                    AxisRYText.Text = "Rotazione Y: nascosta";
                    AxisRZText.Text = "Rotazione Z: nascosta";
                    Slider0Text.Text = "Slider 1: nascosto";
                    Slider1Text.Text = "Slider 2: nascosto";
                }
            }
            catch (Exception ex)
            {
                DeviceStatusText.Text =
                    "Errore durante la lettura: " + ex.Message;
            }
        }

        // =========================================================
        // RILEVAMENTO PULSANTI
        // =========================================================

        private void ProcessButtonCapture(bool[] buttons)
        {
            if (_captureTarget != null)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool wasPressed =
                        _previousButtons != null &&
                        i < _previousButtons.Length &&
                        _previousButtons[i];

                    if (buttons[i] && !wasPressed)
                    {
                        SetPhysicalButton(_captureTarget, i + 1);

                        ButtonCaptureStatusText.Text =
                            $"{_captureTarget} associato a {GetButtonDescription(i)}.";

                        MappingStatusText.Text =
                            "Assegnazione acquisita. Premi APPLICA MAPPATURA per confermare.";

                        _captureTarget = null;
                        break;
                    }
                }
            }

            _previousButtons = (bool[])buttons.Clone();
        }

        private static Dictionary<string, double> GetAxisValues(
            JoystickState state) => new()
        {
            ["X"] = state.X,
            ["Y"] = state.Y,
            ["Z"] = state.Z,
            ["Rotation X"] = state.RotationX,
            ["Rotation Y"] = state.RotationY,
            ["Rotation Z"] = state.RotationZ,
            ["Slider 1"] = state.Sliders[0],
            ["Slider 2"] = state.Sliders[1]
        };

        private void CaptureAxis_Click(object sender, RoutedEventArgs e)
        {
            if (_joystick == null)
            {
                AxisCaptureStatusText.Text =
                    "Collega prima il volante dalla sezione Dispositivi.";
                return;
            }

            if (sender is not Button button || button.Tag is not string target)
                return;

            _captureTarget = null;
            _captureAxisTarget = target;
            _axisCaptureBaseline = new Dictionary<string, double>(_lastAxisValues);

            if (_axisCaptureBaseline.Count == 0)
            {
                AxisCaptureStatusText.Text =
                    "Attendi un istante: sto leggendo gli assi del volante.";

                _captureAxisTarget = null;
                return;
            }

            string label = target switch
            {
                "Steering" => "STERZO: gira il volante a sinistra o a destra",
                "Throttle" => "ACCELERATORE: premi e rilascia soltanto l'acceleratore",
                "Brake" => "FRENO: premi e rilascia soltanto il freno",
                _ => "Muovi il comando da rilevare"
            };

            AxisCaptureStatusText.Text =
                label + ". Rilevamento in corso... Muovi il comando per circa un secondo.";

            MappingStatusText.Text =
                "RILEVA attivo: sto controllando il movimento degli assi.";

            _pollTimer?.Start();
        }

        private void ProcessAxisCapture(Dictionary<string, double> currentAxes)
        {
            if (_captureAxisTarget == null || _axisCaptureBaseline.Count == 0)
                return;

            var movements = currentAxes
                .Where(pair => _axisCaptureBaseline.ContainsKey(pair.Key))
                .Select(pair => new
                {
                    Axis = pair.Key,
                    Delta = Math.Abs(pair.Value - _axisCaptureBaseline[pair.Key])
                })
                .OrderByDescending(item => item.Delta)
                .ToList();

            if (movements.Count == 0)
                return;

            var bestMovement = movements[0];
            string bestAxis = bestMovement.Axis;
            double movement = bestMovement.Delta;

            if (movement < 2500)
                return;

            ComboBox? combo = _captureAxisTarget switch
            {
                "Steering" => SteeringAxisCombo,
                "Throttle" => ThrottleAxisCombo,
                "Brake" => BrakeAxisCombo,
                _ => null
            };

            if (combo != null)
            {
                combo.SelectedItem = bestAxis;

                string command = _captureAxisTarget switch
                {
                    "Steering" => "Sterzo",
                    "Throttle" => "Acceleratore",
                    "Brake" => "Freno",
                    _ => "Comando"
                };

                AxisCaptureStatusText.Text =
                    $"✓ {command}: rilevato asse {bestAxis} (movimento {movement:0}). Premi APPLICA MAPPATURA per confermare.";

                MappingStatusText.Text =
                    $"{command} associato automaticamente all'asse {bestAxis}.";
            }

            _captureAxisTarget = null;
            _axisCaptureBaseline.Clear();
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_joystick == null)
            {
                ButtonCaptureStatusText.Text =
                    "Collega prima il volante dalla sezione Dispositivi.";
                return;
            }

            if (sender is Button button && button.Tag is string target)
                StartButtonCapture(target);
        }

        // Questo metodo sarà usato anche dai pulsanti grafici del controller.
        private void GamepadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string target)
                StartButtonCapture(target);
        }

        private void StartButtonCapture(string target)
        {
            if (_joystick == null)
            {
                ButtonCaptureStatusText.Text =
                    "Collega prima il volante dalla sezione Dispositivi.";
                return;
            }

            _captureAxisTarget = null;
            _axisCaptureBaseline.Clear();
            _captureTarget = target;

            ButtonCaptureStatusText.Text =
                $"Premi ora il pulsante fisico da associare a {target}...";

            UpdateGamepadVisuals(_previousButtons ?? Array.Empty<bool>());
        }

        private void SetPhysicalButton(string target, int physicalButton)
        {
            ComboBox? combo = target switch
            {
                "A" => ButtonACombo,
                "B" => ButtonBCombo,
                "X" => ButtonXCombo,
                "Y" => ButtonYCombo,
                "LB" => ButtonLBCombo,
                "RB" => ButtonRBCombo,
                "Start" => ButtonStartCombo,
                "Back" => ButtonBackCombo,
                "L3" => ButtonL3Combo,
                "R3" => ButtonR3Combo,
                "DPadUp" => ButtonDPadUpCombo,
                "DPadDown" => ButtonDPadDownCombo,
                "DPadLeft" => ButtonDPadLeftCombo,
                "DPadRight" => ButtonDPadRightCombo,
                _ => null
            };

            if (combo != null)
                combo.SelectedIndex = Math.Clamp(physicalButton, 0, 144);
        }

        // =========================================================
        // ASSI, MONITOR E CONTROLLER XBOX 360 VIRTUALE
        // =========================================================

        private static double NormalizeAxis(double raw)
        {
            double normalized = raw >= 0 && raw <= 65535
                ? raw / 65535.0
                : (raw + 32768.0) / 65535.0;

            return Math.Clamp(normalized, 0.0, 1.0);
        }

        private static double ApplyPedalDeadzone(double value, double deadzone)
        {
            if (value <= deadzone)
                return 0;

            return Math.Clamp(
                (value - deadzone) / (1.0 - deadzone),
                0.0,
                1.0);
        }

        private void UpdateMappedMonitor(JoystickState state)
        {
            var profile = ReadCurrentProfile();

            double deadzone = Math.Clamp(
                profile.DeadzonePercent / 100.0, 0.0, 0.30);

            // Ora il monitor supporta tutta la scala fino al 1000%.
            double sensitivity = Math.Clamp(
                profile.SensitivityPercent / 100.0, 0.5, 10.0);

            double steering =
                NormalizeAxis(ReadAxis(state, profile.SteeringAxis));

            if (profile.InvertSteering)
                steering = 1.0 - steering;

            double centered = (steering - 0.5) * 2.0;

            if (Math.Abs(centered) <= deadzone)
                centered = 0;
            else
                centered = Math.Sign(centered) *
                    ((Math.Abs(centered) - deadzone) / (1.0 - deadzone));

            // Stessa risposta progressiva usata dal controller virtuale.
            double exponent = 1.0 / sensitivity;

            centered = Math.Sign(centered) *
                Math.Pow(Math.Abs(centered), exponent);

            centered = Math.Clamp(centered, -1.0, 1.0);

            double steeringBar = 50.0 + centered * 50.0;

            double throttle =
                NormalizeAxis(ReadAxis(state, profile.ThrottleAxis));

            double brake =
                NormalizeAxis(ReadAxis(state, profile.BrakeAxis));

            if (profile.InvertThrottle)
                throttle = 1.0 - throttle;

            if (profile.InvertBrake)
                brake = 1.0 - brake;

            throttle = ApplyPedalDeadzone(throttle, deadzone);
            brake = ApplyPedalDeadzone(brake, deadzone);

            DashboardSteeringBar.Value = steeringBar;
            DashboardThrottleBar.Value = throttle * 100.0;
            DashboardBrakeBar.Value = brake * 100.0;

            DashboardSteeringValue.Text = $"{Math.Round(centered * 100):0}%";
            DashboardThrottleValue.Text = $"{Math.Round(throttle * 100):0}%";
            DashboardBrakeValue.Text = $"{Math.Round(brake * 100):0}%";

            MappingSteeringMeter.Value = steeringBar;
            MappingThrottleMeter.Value = throttle * 100.0;
            MappingBrakeMeter.Value = brake * 100.0;

            MappingSteeringValue.Text = $"{Math.Round(centered * 100):0}%";
            MappingThrottleValue.Text = $"{Math.Round(throttle * 100):0}%";
            MappingBrakeValue.Text = $"{Math.Round(brake * 100):0}%";
        }

        private void UpdateVirtualController(JoystickState state)
        {
            if (_virtualController == null)
                return;

            var profile = ReadCurrentProfile();
            var allButtonStates = GetCombinedButtonStates(state);

            double deadzone = Math.Clamp(
                profile.DeadzonePercent / 100.0, 0.0, 0.30);

            double sensitivity = Math.Clamp(
                profile.SensitivityPercent / 100.0, 0.5, 10.0);

            // STERZO
            double steering =
                NormalizeAxis(ReadAxis(state, profile.SteeringAxis));

            if (profile.InvertSteering)
                steering = 1.0 - steering;

            double centered = (steering - 0.5) * 2.0;

            if (Math.Abs(centered) <= deadzone)
                centered = 0;
            else
                centered = Math.Sign(centered) *
                    ((Math.Abs(centered) - deadzone) / (1.0 - deadzone));

            double steeringExponent = 1.0 / sensitivity;

            centered = Math.Sign(centered) *
                Math.Pow(Math.Abs(centered), steeringExponent);

            centered = Math.Clamp(centered, -1.0, 1.0);

            short steeringValue = (short)Math.Round(
                centered < 0
                    ? centered * 32768.0
                    : centered * 32767.0);

            // ACCELERATORE
            double throttle =
                NormalizeAxis(ReadAxis(state, profile.ThrottleAxis));

            if (profile.InvertThrottle)
                throttle = 1.0 - throttle;

            throttle = ApplyPedalDeadzone(throttle, deadzone);

            // FRENO
            double brake =
                NormalizeAxis(ReadAxis(state, profile.BrakeAxis));

            if (profile.InvertBrake)
                brake = 1.0 - brake;

            brake = ApplyPedalDeadzone(brake, deadzone);

            byte throttleValue = (byte)Math.Round(throttle * 255.0);
            byte brakeValue = (byte)Math.Round(brake * 255.0);

            // STERZO: levetta sinistra X.
            _virtualController.SetAxisValue(
                Xbox360Axis.LeftThumbX, steeringValue);

            // PEDALI: RT = acceleratore, LT = freno.
            _virtualController.SetSliderValue(
                Xbox360Slider.RightTrigger, throttleValue);

            _virtualController.SetSliderValue(
                Xbox360Slider.LeftTrigger, brakeValue);

            // PULSANTI PRINCIPALI
            _virtualController.SetButtonState(
                Xbox360Button.A,
                IsMappedButtonPressed(ButtonACombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.B,
                IsMappedButtonPressed(ButtonBCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.X,
                IsMappedButtonPressed(ButtonXCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.Y,
                IsMappedButtonPressed(ButtonYCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.LeftShoulder,
                IsMappedButtonPressed(ButtonLBCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.RightShoulder,
                IsMappedButtonPressed(ButtonRBCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.Start,
                IsMappedButtonPressed(ButtonStartCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.Back,
                IsMappedButtonPressed(ButtonBackCombo, allButtonStates));

            // LEVETTE PREMIBILI
            _virtualController.SetButtonState(
                Xbox360Button.LeftThumb,
                IsMappedButtonPressed(ButtonL3Combo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.RightThumb,
                IsMappedButtonPressed(ButtonR3Combo, allButtonStates));

            // CROCE DIREZIONALE
            _virtualController.SetButtonState(
                Xbox360Button.Up,
                IsMappedButtonPressed(ButtonDPadUpCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.Down,
                IsMappedButtonPressed(ButtonDPadDownCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.Left,
                IsMappedButtonPressed(ButtonDPadLeftCombo, allButtonStates));

            _virtualController.SetButtonState(
                Xbox360Button.Right,
                IsMappedButtonPressed(ButtonDPadRightCombo, allButtonStates));

            _virtualController.SubmitReport();
        }

        private static bool IsMappedButtonPressed(ComboBox combo, bool[] buttons)
        {
            int physicalIndex = combo.SelectedIndex - 1;

            return physicalIndex >= 0 &&
                   physicalIndex < buttons.Length &&
                   buttons[physicalIndex];
        }

        private static double ReadAxis(JoystickState state, string axis)
        {
            return axis switch
            {
                "X" => state.X,
                "Y" => state.Y,
                "Z" => state.Z,
                "Rotation X" => state.RotationX,
                "Rotation Y" => state.RotationY,
                "Rotation Z" => state.RotationZ,
                "Slider 1" => state.Sliders[0],
                "Slider 2" => state.Sliders[1],
                _ => state.X
            };
        }

        // =========================================================
        // DISCONNESSIONE
        // =========================================================

        private void DisconnectJoystick()
        {
            _captureTarget = null;
            _captureAxisTarget = null;

            _axisCaptureBaseline.Clear();
            _lastAxisValues.Clear();

            _pollTimer?.Stop();
            _pollTimer = null;

            _joystick?.Dispose();
            _joystick = null;

            _previousButtons = null;

            AxisXText.Text = "Asse X: —";
            AxisYText.Text = "Asse Y: —";
            AxisZText.Text = "Asse Z: —";
            AxisRXText.Text = "Rotazione X: —";
            AxisRYText.Text = "Rotazione Y: —";
            AxisRZText.Text = "Rotazione Z: —";

            Slider0Text.Text = "Slider 1: —";
            Slider1Text.Text = "Slider 2: —";
            ButtonsText.Text = "Nessun pulsante rilevato.";

            DashboardSteeringBar.Value = 50;
            DashboardThrottleBar.Value = 0;
            DashboardBrakeBar.Value = 0;

            DashboardSteeringValue.Text = "0%";
            DashboardThrottleValue.Text = "0%";
            DashboardBrakeValue.Text = "0%";

            MappingSteeringMeter.Value = 50;
            MappingThrottleMeter.Value = 0;
            MappingBrakeMeter.Value = 0;

            MappingSteeringValue.Text = "0%";
            MappingThrottleValue.Text = "0%";
            MappingBrakeValue.Text = "0%";

            DashboardDeviceStatus.Text = "Non collegato";
            DashboardDeviceStatus.Foreground =
                new SolidColorBrush(Color.FromRgb(234, 184, 102));

            DashboardInputStatus.Text =
                "Collega un dispositivo dalla sezione Dispositivi.";

            ButtonCaptureStatusText.Text =
                "Per usare RILEVA, collega prima il volante.";

            GamepadSelectionText.Text = "Controller non collegato.";

            UpdateGamepadVisuals(Array.Empty<bool>());
        }

        private void DisconnectDevice_Click(object sender, RoutedEventArgs e)
        {
            DisconnectJoystick();
            DeviceStatusText.Text = "Dispositivo disconnesso.";
        }

        // =========================================================
        // PROFILI E MAPPATURA
        // =========================================================

        private WheelProfile ReadCurrentProfile()
        {
            return new WheelProfile
            {
                Name = string.IsNullOrWhiteSpace(ProfileNameTextBox.Text)
                    ? "Il mio profilo"
                    : ProfileNameTextBox.Text.Trim(),

                SteeringAxis = SteeringAxisCombo.SelectedItem as string ?? "X",
                ThrottleAxis = ThrottleAxisCombo.SelectedItem as string ?? "Y",
                BrakeAxis = BrakeAxisCombo.SelectedItem as string ?? "Z",

                InvertSteering = InvertSteeringCheck.IsChecked.GetValueOrDefault(),
                InvertThrottle = InvertThrottleCheck.IsChecked.GetValueOrDefault(),
                InvertBrake = InvertBrakeCheck.IsChecked.GetValueOrDefault(),

                SensitivityPercent = (int)Math.Round(SensitivitySlider.Value),
                DeadzonePercent = (int)Math.Round(DeadzoneSlider.Value),
                ShowRawValues = ShowRawValuesCheck.IsChecked.GetValueOrDefault(),

                ButtonA = ButtonACombo.SelectedIndex,
                ButtonB = ButtonBCombo.SelectedIndex,
                ButtonX = ButtonXCombo.SelectedIndex,
                ButtonY = ButtonYCombo.SelectedIndex,
                ButtonLB = ButtonLBCombo.SelectedIndex,
                ButtonRB = ButtonRBCombo.SelectedIndex,
                ButtonStart = ButtonStartCombo.SelectedIndex,
                ButtonBack = ButtonBackCombo.SelectedIndex,

                ButtonL3 = ButtonL3Combo.SelectedIndex,
                ButtonR3 = ButtonR3Combo.SelectedIndex,
                ButtonDPadUp = ButtonDPadUpCombo.SelectedIndex,
                ButtonDPadDown = ButtonDPadDownCombo.SelectedIndex,
                ButtonDPadLeft = ButtonDPadLeftCombo.SelectedIndex,
                ButtonDPadRight = ButtonDPadRightCombo.SelectedIndex
            };
        }

        private void ApplyProfile(WheelProfile profile)
        {
            SteeringAxisCombo.SelectedItem =
                AxisNames.Contains(profile.SteeringAxis) ? profile.SteeringAxis : "X";

            ThrottleAxisCombo.SelectedItem =
                AxisNames.Contains(profile.ThrottleAxis) ? profile.ThrottleAxis : "Y";

            BrakeAxisCombo.SelectedItem =
                AxisNames.Contains(profile.BrakeAxis) ? profile.BrakeAxis : "Z";

            InvertSteeringCheck.IsChecked = profile.InvertSteering;
            InvertThrottleCheck.IsChecked = profile.InvertThrottle;
            InvertBrakeCheck.IsChecked = profile.InvertBrake;

            SensitivitySlider.Value =
                Math.Clamp(profile.SensitivityPercent, 50, 1000);

            DeadzoneSlider.Value =
                Math.Clamp(profile.DeadzonePercent, 0, 30);

            ShowRawValuesCheck.IsChecked = profile.ShowRawValues;

            SetButtonComboIndex(ButtonACombo, profile.ButtonA);
            SetButtonComboIndex(ButtonBCombo, profile.ButtonB);
            SetButtonComboIndex(ButtonXCombo, profile.ButtonX);
            SetButtonComboIndex(ButtonYCombo, profile.ButtonY);
            SetButtonComboIndex(ButtonLBCombo, profile.ButtonLB);
            SetButtonComboIndex(ButtonRBCombo, profile.ButtonRB);
            SetButtonComboIndex(ButtonStartCombo, profile.ButtonStart);
            SetButtonComboIndex(ButtonBackCombo, profile.ButtonBack);

            SetButtonComboIndex(ButtonL3Combo, profile.ButtonL3);
            SetButtonComboIndex(ButtonR3Combo, profile.ButtonR3);
            SetButtonComboIndex(ButtonDPadUpCombo, profile.ButtonDPadUp);
            SetButtonComboIndex(ButtonDPadDownCombo, profile.ButtonDPadDown);
            SetButtonComboIndex(ButtonDPadLeftCombo, profile.ButtonDPadLeft);
            SetButtonComboIndex(ButtonDPadRightCombo, profile.ButtonDPadRight);

            DashboardProfileName.Text = profile.Name;
            ProfileNameTextBox.Text = profile.Name;

            if (_joystick != null)
                PollJoystick();
            else
                UpdateGamepadVisuals(Array.Empty<bool>());
        }

        private static void SetButtonComboIndex(ComboBox combo, int index)
        {
            combo.SelectedIndex = Math.Clamp(index, 0, 144);
        }

        private void ApplyMapping_Click(object sender, RoutedEventArgs e)
        {
            MappingStatusText.Text =
                "Mappatura applicata al monitor WheelBridge. Salva un profilo per conservarla.";

            if (_joystick != null)
                PollJoystick();
            else
                UpdateGamepadVisuals(Array.Empty<bool>());
        }

        // =========================================================
        // IMPOSTAZIONI
        // =========================================================

        private string SettingsFilePath =>
            Path.Combine(_appFolder, "settings.json");

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return;

                var settings = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(SettingsFilePath));

                if (settings == null)
                    return;

                SensitivitySlider.Value =
                    Math.Clamp(settings.SensitivityPercent, 50, 1000);

                DeadzoneSlider.Value =
                    Math.Clamp(settings.DeadzonePercent, 0, 30);

                ShowRawValuesCheck.IsChecked = settings.ShowRawValues;
            }
            catch (Exception ex)
            {
                SettingsStatusText.Text =
                    "Impossibile leggere le impostazioni: " + ex.Message;
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new AppSettings
                {
                    SensitivityPercent = (int)Math.Round(SensitivitySlider.Value),
                    DeadzonePercent = (int)Math.Round(DeadzoneSlider.Value),
                    ShowRawValues = ShowRawValuesCheck.IsChecked.GetValueOrDefault()
                };

                Directory.CreateDirectory(_appFolder);

                File.WriteAllText(
                    SettingsFilePath,
                    JsonSerializer.Serialize(settings, _jsonOptions));

                SettingsStatusText.Text =
                    "Impostazioni salvate correttamente su questo PC.";
            }
            catch (Exception ex)
            {
                SettingsStatusText.Text =
                    "Errore nel salvataggio: " + ex.Message;
            }
        }

        private void SensitivitySlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (SensitivityValueText != null)
                SensitivityValueText.Text = $"{(int)Math.Round(e.NewValue)}%";

            if (MappingSensitivitySlider != null &&
                Math.Abs(MappingSensitivitySlider.Value - e.NewValue) > 0.001)
            {
                MappingSensitivitySlider.Value = e.NewValue;
            }
        }

        private void MappingSensitivitySlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (MappingSensitivityValueText != null)
                MappingSensitivityValueText.Text = $"{(int)Math.Round(e.NewValue)}%";

            if (SensitivitySlider != null &&
                Math.Abs(SensitivitySlider.Value - e.NewValue) > 0.001)
            {
                SensitivitySlider.Value = e.NewValue;
            }
        }

        private void DeadzoneSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (DeadzoneValueText != null)
                DeadzoneValueText.Text = $"{(int)Math.Round(e.NewValue)}%";
        }

        // =========================================================
        // GESTIONE PROFILI
        // =========================================================

        private void RefreshProfiles()
        {
            ProfilesListBox.Items.Clear();

            try
            {
                Directory.CreateDirectory(_profilesFolder);

                foreach (var file in Directory.GetFiles(
                    _profilesFolder, "*.json").OrderBy(p => p))
                {
                    try
                    {
                        var profile = JsonSerializer.Deserialize<WheelProfile>(
                            File.ReadAllText(file));

                        if (profile != null)
                        {
                            ProfilesListBox.Items.Add(new ProfileListEntry
                            {
                                Name = profile.Name,
                                FilePath = file
                            });
                        }
                    }
                    catch
                    {
                        // Ignora un profilo danneggiato.
                    }
                }
            }
            catch (Exception ex)
            {
                ProfileStatusText.Text =
                    "Errore nella lettura dei profili: " + ex.Message;
            }
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = ProfileNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ProfileStatusText.Text = "Inserisci un nome per il profilo.";
                return;
            }

            try
            {
                var existing = ProfilesListBox.Items
                    .Cast<ProfileListEntry>()
                    .FirstOrDefault(p => string.Equals(
                        p.Name, name, StringComparison.OrdinalIgnoreCase));

                string filePath = existing?.FilePath ??
                    Path.Combine(_profilesFolder, Guid.NewGuid().ToString("N") + ".json");

                if (existing != null)
                {
                    var result = MessageBox.Show(
                        $"Il profilo '{name}' esiste già. Vuoi sovrascriverlo?",
                        "WheelBridge",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                var profile = ReadCurrentProfile();
                profile.Name = name;

                File.WriteAllText(
                    filePath,
                    JsonSerializer.Serialize(profile, _jsonOptions));

                RefreshProfiles();
                SelectProfileByPath(filePath);

                DashboardProfileName.Text = name;
                ProfileStatusText.Text =
                    $"Profilo '{name}' salvato correttamente.";
            }
            catch (Exception ex)
            {
                ProfileStatusText.Text =
                    "Errore nel salvataggio del profilo: " + ex.Message;
            }
        }

        private void LoadProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is not ProfileListEntry selected)
            {
                ProfileStatusText.Text = "Seleziona prima un profilo.";
                return;
            }

            try
            {
                var profile = JsonSerializer.Deserialize<WheelProfile>(
                    File.ReadAllText(selected.FilePath));

                if (profile == null)
                    throw new InvalidDataException(
                        "Il file del profilo è vuoto o non valido.");

                ApplyProfile(profile);

                ProfileStatusText.Text =
                    $"Profilo '{profile.Name}' caricato. La mappatura aggiorna il monitor WheelBridge.";
            }
            catch (Exception ex)
            {
                ProfileStatusText.Text =
                    "Errore nel caricamento: " + ex.Message;
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is not ProfileListEntry selected)
            {
                ProfileStatusText.Text =
                    "Seleziona prima un profilo da eliminare.";
                return;
            }

            var answer = MessageBox.Show(
                $"Eliminare il profilo '{selected.Name}'?",
                "WheelBridge",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                File.Delete(selected.FilePath);
                RefreshProfiles();

                ProfileStatusText.Text =
                    $"Profilo '{selected.Name}' eliminato.";
            }
            catch (Exception ex)
            {
                ProfileStatusText.Text =
                    "Errore durante l'eliminazione: " + ex.Message;
            }
        }

        private void SelectProfileByPath(string filePath)
        {
            foreach (var item in ProfilesListBox.Items)
            {
                if (item is ProfileListEntry entry &&
                    string.Equals(
                        entry.FilePath,
                        filePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ProfilesListBox.SelectedItem = item;
                    break;
                }
            }
        }

        // =========================================================
        // CHIUSURA APPLICAZIONE
        // =========================================================

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            DisconnectJoystick();

            _virtualController?.Disconnect();
            _virtualController = null;

            _vigemClient?.Dispose();
            _vigemClient = null;

            _directInput?.Dispose();
            _directInput = null;
        }
    }
}
