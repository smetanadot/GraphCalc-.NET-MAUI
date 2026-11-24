using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text;
using CommunityToolkit.Maui.Storage;
using System.Text.RegularExpressions;




namespace DESMOS
{
    public partial class MainPage : ContentPage
    {
        private GraphDrawable _graphDrawable;
        private ObservableCollection<GraphFunction> _functions;

        private ObservableCollection<GraphParameter> _parameters;

        private static readonly HashSet<string> _reservedIdentifiers = new(StringComparer.OrdinalIgnoreCase)
        {
            "x", "Sin", "Cos", "Tan", "Asin", "Acos", "Atan",
            "Sqrt", "Abs", "Exp", "Log", "Log10", "Pow",
            "Floor", "Ceiling", "Round", "Max", "Min", "Sign",
            "Pi", "E"
        };


        private readonly Color[] _palette =
        {
            Colors.Blue, Colors.Red, Colors.Green, Colors.Orange, Colors.Purple, Colors.Teal
        };

        private float _savedOffsetX = 0f;
        private float _savedOffsetY = 0f;
        private float _startPinchScale = 1f;

        // Путь к файлу сохранения в папке данных приложения
        private string SaveFilePath => Path.Combine(FileSystem.AppDataDirectory, "desmos_project.json");

        public MainPage()
        {

            InitializeComponent();

            _functions = new ObservableCollection<GraphFunction>
            {
                new GraphFunction { Formula = "Sin(x)", LineColor = _palette[0] },
                new GraphFunction { Formula = "Cos(x)", LineColor = _palette[1] }
            };

            _graphDrawable = new GraphDrawable { Functions = _functions };
            GraphView.Drawable = _graphDrawable;
            FunctionsList.ItemsSource = _functions;

            _parameters = new ObservableCollection<GraphParameter>();
            _graphDrawable.Parameters = _parameters;
            ParametersList.ItemsSource = _parameters;


        }

        private void SyncParametersFromFormulas()
        {
            var foundNames = new HashSet<string>();

            foreach (var func in _functions)
            {
                if (string.IsNullOrWhiteSpace(func.Formula)) continue;

                var matches = Regex.Matches(func.Formula, "[a-zA-Z_][a-zA-Z0-9_]*");
                foreach (Match match in matches)
                {
                    if (!_reservedIdentifiers.Contains(match.Value))
                        foundNames.Add(match.Value);
                }
            }

            foreach (var name in foundNames)
            {
                if (!_parameters.Any(p => p.Name == name))
                    _parameters.Add(new GraphParameter { Name = name, Value = 1.0, Min = -10, Max = 10 });
            }

            for (int i = _parameters.Count - 1; i >= 0; i--)
            {
                if (!foundNames.Contains(_parameters[i].Name))
                    _parameters.RemoveAt(i);
            }
        }


        //СОХРАНЕНИЕ
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // Собираем текущее состояние экрана в один объект
            var project = new GraphProjectData
            {
                Scale = _graphDrawable.Scale,
                OffsetX = _graphDrawable.OffsetX,
                OffsetY = _graphDrawable.OffsetY,
                Functions = _functions.ToList(),
                Parameters = _parameters.ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(project, options);


            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var result = await FileSaver.Default.SaveAsync("my_graph.json", stream, CancellationToken.None);

            if (result.IsSuccessful)
            {
                await DisplayAlert("Сохранено", $"График сохранён в файл:\n{result.FilePath}", "OK");
            }
            else if (result.Exception != null)
            {
                await DisplayAlert("Ошибка сохранения", result.Exception.Message, "OK");
            }
        }

        //ЗАГРУЗКА

        // Открываем окошко DragnDrop по нажатию кнопки

        private async Task LoadProjectFromStreamAsync(Stream stream)
        {
            try
            {
                using var reader = new StreamReader(stream);
                string json = await reader.ReadToEndAsync();

                var project = JsonSerializer.Deserialize<GraphProjectData>(json);
                if (project == null) return;

                _graphDrawable.Scale = project.Scale;
                _graphDrawable.OffsetX = _savedOffsetX = project.OffsetX;
                _graphDrawable.OffsetY = _savedOffsetY = project.OffsetY;

                _parameters.Clear();
                if (project.Parameters != null)
                {
                    foreach (var param in project.Parameters)
                        _parameters.Add(param);
                }

                _functions.Clear();
                if (project.Functions != null)
                {
                    foreach (var func in project.Functions)
                        _functions.Add(func);
                }

                SyncParametersFromFormulas();
                GraphView.Invalidate();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка загрузки", $"Не удалось прочитать файл: {ex.Message}", "OK");
            }
        }


        private void OnLoadClicked(object sender, EventArgs e)
        {
            LoadOverlay.IsVisible = true;
        }

        // Закрываем окошко по кнопке "Отмена"
        private void OnCloseLoadOverlayClicked(object sender, EventArgs e)
        {
            LoadOverlay.IsVisible = false;
        }

        // Если пользователь всё-таки нажал
        private async void OnBrowseFileClicked(object sender, EventArgs e)
        {
            var jsonFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, new[] { ".json" } },
        { DevicePlatform.macOS, new[] { "public.json" } },
        { DevicePlatform.Android, new[] { "application/json" } },
        { DevicePlatform.iOS, new[] { "public.json" } },
    });

            var pickResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Выберите файл графика (.json)",
                FileTypes = jsonFileType
            });

            if (pickResult == null) return;

            using var stream = await pickResult.OpenReadAsync();
            await LoadProjectFromStreamAsync(stream);
            LoadOverlay.IsVisible = false;
        }


        //Подключаем приём внешних файлов из Проводника Windows для рамки DropZoneBorder
        private void OnDropZoneHandlerChanged(object sender, EventArgs e)
        {
#if WINDOWS
    if (DropZoneBorder.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement nativeBorder)
    {
        nativeBorder.AllowDrop = true;

        nativeBorder.DragOver += (s, args) =>
        {
            args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            args.DragUIOverride.Caption = "Отпустите, чтобы загрузить график";
            args.DragUIOverride.IsCaptionVisible = true;
            DropZoneBorder.BackgroundColor = Color.FromArgb("#EFF6FF"); // Подсветка рамки при наведении
        };

        nativeBorder.DragLeave += (s, args) =>
        {
            DropZoneBorder.BackgroundColor = Colors.White;
        };

        nativeBorder.Drop += async (s, args) =>
        {
            DropZoneBorder.BackgroundColor = Colors.White;

            if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await args.DataView.GetStorageItemsAsync();
                if (items.FirstOrDefault() is Windows.Storage.StorageFile file)
                {
                    if (!file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        await DisplayAlert("Неверный формат", "Пожалуйста, перетащите файл с расширением .json", "OK");
                        return;
                    }

                    using var winStream = await file.OpenStreamForReadAsync();
                    await LoadProjectFromStreamAsync(winStream);
                    LoadOverlay.IsVisible = false;
                }
            }
        };
    }
#endif
        }

        private void OnFileDropped(object sender, DropEventArgs e)
        {
            // На Windows сработает нативный Drop из OnDropZoneHandlerChanged
        }


        // Запасные обработчики MAUI DropGestureRecognizer (для macOS / внутренних перетаскиваний)
        private void OnFileDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }


        //ЗУМ КОЛЁСИКОМ МЫШИ

        private void OnGraphViewHandlerChanged(object sender, EventArgs e)
        {
#if WINDOWS
            if (GraphView.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement nativeView)
            {
                nativeView.PointerWheelChanged += (s, args) =>
                {
                    var point = args.GetCurrentPoint(nativeView);
                    int delta = point.Properties.MouseWheelDelta;

                    // delta > 0 — колесо вверх (приближаем), delta < 0 — вниз (отдаляем)
                    float zoomFactor = delta > 0 ? 1.15f : (1f / 1.15f);
                    ApplyZoom(zoomFactor);

                    args.Handled = true;
                };
            }
#endif
        }

        //ЗУМ ЩИПКОМ

        private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status == GestureStatus.Started)
            {
                _startPinchScale = _graphDrawable.Scale;
            }
            else if (e.Status == GestureStatus.Running)
            {
                float newScale = _startPinchScale * (float)e.Scale;
                float zoomFactor = newScale / _graphDrawable.Scale;
                ApplyZoom(zoomFactor);
            }
        }

        //Общий метод масштабирования (используется кнопками, колесом и щипком)
        private void ApplyZoom(float zoomFactor)
        {
            //Ограничиваем слишком сильный или слабый зум
            float targetScale = _graphDrawable.Scale * zoomFactor;
            if (targetScale < 2f || targetScale > 50000f) return;

            _savedOffsetX *= zoomFactor;
            _savedOffsetY *= zoomFactor;
            _graphDrawable.OffsetX = _savedOffsetX;
            _graphDrawable.OffsetY = _savedOffsetY;
            _graphDrawable.Scale = targetScale;

            GraphView.Invalidate();
        }

        // --- ОСТАЛЬНЫЕ ОБРАБОТЧИКИ ---

        private void OnZoomInClicked(object sender, EventArgs e) => ApplyZoom(1.2f);
        private void OnZoomOutClicked(object sender, EventArgs e) => ApplyZoom(1f / 1.2f);

        private void OnAddFunctionClicked(object sender, EventArgs e)
        {
            var nextColor = _palette[_functions.Count % _palette.Length];
            _functions.Add(new GraphFunction { Formula = "", LineColor = nextColor });
        }

        private void OnRemoveFunctionClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is GraphFunction func)
            {
                _functions.Remove(func);
                SyncParametersFromFormulas();
                GraphView.Invalidate();
            }
        }

        private void OnParameterSliderChanged(object sender, ValueChangedEventArgs e)
        {
            GraphView.Invalidate();
        }


        private void OnColorClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is GraphFunction func)
            {
                string currentHex = func.LineColor.ToArgbHex(true);
                int currentIndex = Array.FindIndex(_palette, c => c.ToArgbHex(true) == currentHex);

                int nextIndex = (currentIndex + 1) % _palette.Length;
                func.LineColor = _palette[nextIndex]; 
                GraphView.Invalidate();
            }
        }

        private void OnFormulaTextChanged(object sender, TextChangedEventArgs e)
        {
            SyncParametersFromFormulas();
            GraphView.Invalidate();
        }

        private void OnGraphViewSizeChanged(object sender, EventArgs e) => GraphView.Invalidate();

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Running:
                    _graphDrawable.OffsetX = _savedOffsetX + (float)e.TotalX;
                    _graphDrawable.OffsetY = _savedOffsetY + (float)e.TotalY;
                    GraphView.Invalidate();
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    _savedOffsetX = _graphDrawable.OffsetX;
                    _savedOffsetY = _graphDrawable.OffsetY;
                    break;
            }
        }

        //private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        //{
        //    var pos = e.GetPosition(GraphView);
        //    System.Diagnostics.Debug.WriteLine($"[TAP] position = {pos}");

        //    _graphDrawable.SelectedMathX = pos.Value.X;
        //    _graphDrawable.SelectedMathY = pos.Value.Y;

        //    GraphView.Invalidate();



        //}
    }
}
