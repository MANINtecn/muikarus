using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Data.Texture;

namespace MuAndroid
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Exported = true,
        Icon = "@drawable/icon",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.Landscape,
        ConfigurationChanges =
            ConfigChanges.Orientation |
            ConfigChanges.Keyboard |
            ConfigChanges.KeyboardHidden |
            ConfigChanges.ScreenSize)]
    public class MainActivity : AndroidGameActivity
    {
        private Client.Main.MuGame _game;
        private View _view;

        const int RequestWrite = 101;
        private void RequestLegacyWritePermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M &&
                Build.VERSION.SdkInt <= BuildVersionCodes.P &&
                CheckSelfPermission(Manifest.Permission.WriteExternalStorage)
                    != Permission.Granted)
            {
                RequestPermissions(
                    new[] { Manifest.Permission.WriteExternalStorage },
                    RequestWrite);
            }
        }

        private static string SaveCrashLog(string text)
        {
            var ctx = Application.Context!;
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var name = $"MuAndroid_crash_{stamp}.txt";
            var dirPath = Android.OS.Environment
                            .GetExternalStoragePublicDirectory(
                                Android.OS.Environment.DirectoryDownloads)
                            .AbsolutePath;
            var filePath = Path.Combine(dirPath, name);

             try
            {
                Directory.CreateDirectory(dirPath);
                File.AppendAllText(filePath, text + System.Environment.NewLine);
                return filePath;
            }
            catch
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    var values = new ContentValues();
                    values.Put(MediaStore.IMediaColumns.DisplayName, name);
                    values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
                    values.Put(MediaStore.MediaColumns.RelativePath,
                               Android.OS.Environment.DirectoryDownloads);

                    var uri = ctx.ContentResolver!
                                   .Insert(MediaStore.Downloads.ExternalContentUri, values);

                    using var stream = ctx.ContentResolver.OpenOutputStream(uri!)!;
                    using var sw = new StreamWriter(stream);
                    sw.Write(text);

                    return $"/storage/emulated/0/Download/{name}";
                }

                return "FAILED";
            }
        }

        private void ApplyAndroidDefaults()
        {
            Client.Main.Constants.DRAW_GRASS = false;
            Client.Main.Constants.ENABLE_DYNAMIC_LIGHTS = false;
            Client.Main.Constants.ENABLE_TERRAIN_GPU_LIGHTING = false;
            Client.Main.Constants.OPTIMIZE_FOR_INTEGRATED_GPU = true;
            Client.Main.Constants.HIGH_QUALITY_TEXTURES = false;
            Client.Main.Constants.RENDER_SCALE = 1.0f;
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            RequestLegacyWritePermission();

            ApplyAndroidDefaults();

            TextureLoader.Instance.CustomDecompressFunction = (textureInfo) =>
            {
                if (textureInfo.Format == TextureSurfaceFormat.Dxt1)
                    return DxtDecoder.DecompressDXT1(textureInfo.Data, (int)textureInfo.Width, (int)textureInfo.Height);
                if (textureInfo.Format == TextureSurfaceFormat.Dxt3)
                    return DxtDecoder.DecompressDXT3(textureInfo.Data, (int)textureInfo.Width, (int)textureInfo.Height);
                if (textureInfo.Format == TextureSurfaceFormat.Dxt5)
                    return DxtDecoder.DecompressDXT5(textureInfo.Data, (int)textureInfo.Width, (int)textureInfo.Height);
                return null;
            };

            InitializeKeyboardBridge();

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var msg = $"Global Exception:\n{(Exception)e.ExceptionObject}";
                var path = SaveCrashLog(msg);
                Android.Util.Log.Error("MuAndroidCrash", $"{msg}\nSaved: {path}");
            };

            _game = new Client.Main.MuGame();

            if (!Directory.Exists(Client.Main.Constants.DataPath))
                Directory.CreateDirectory(Client.Main.Constants.DataPath);

            _view = (View)_game.Services.GetService(typeof(View));
            SetContentView(_view);
            _game.Run();
        }

        private EditText _hiddenInput;
        private static Client.Main.Controls.UI.TextFieldControl _activeField;

        private void InitializeKeyboardBridge()
        {
            _hiddenInput = new EditText(this);
            _hiddenInput.Alpha = 0.01f;
            _hiddenInput.Background = null;
            _hiddenInput.SetX(-500);
            _hiddenInput.SetY(-500);
            var layoutParams = new ViewGroup.LayoutParams(1, 1);
            AddContentView(_hiddenInput, layoutParams);

            _hiddenInput.TextChanged += (s, e) =>
            {
                if (_activeField != null)
                {
                    string newText = _hiddenInput.Text ?? string.Empty;
                    if (_activeField.Value != newText)
                    {
                        Client.Main.MuGame.ScheduleOnMainThread(() =>
                        {
                            if (_activeField != null)
                            {
                                _activeField.Value = newText;
                            }
                        });
                    }
                }
            };

            _hiddenInput.EditorAction += (s, e) =>
            {
                if (e.ActionId == ImeAction.Done || e.ActionId == ImeAction.Go || e.ActionId == ImeAction.Send || e.Event?.KeyCode == Keycode.Enter)
                {
                    HideKeyboard();
                }
            };

            Client.Main.Controls.UI.TextFieldControl.OnFieldFocused = (control) =>
            {
                RunOnUiThread(() =>
                {
                    try
                    {
                        if (_activeField == control)
                            return;

                        _activeField = control;

                        if (control.MaskValue)
                        {
                            _hiddenInput.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;
                            _hiddenInput.TransformationMethod = Android.Text.Method.PasswordTransformationMethod.Instance;
                        }
                        else
                        {
                            _hiddenInput.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationVisiblePassword;
                            _hiddenInput.TransformationMethod = null;
                        }

                        _hiddenInput.Text = control.Value ?? string.Empty;
                        if (!string.IsNullOrEmpty(_hiddenInput.Text))
                        {
                            _hiddenInput.SetSelection(_hiddenInput.Text.Length);
                        }

                        _hiddenInput.RequestFocus();
                        var imm = (InputMethodManager)GetSystemService(InputMethodService);
                        imm?.ShowSoftInput(_hiddenInput, ShowFlags.Forced);
                    }
                    catch (Exception ex)
                    {
                        Android.Util.Log.Error("MuAndroid", $"Error focusing hidden input: {ex}");
                    }
                });
            };

            Client.Main.Controls.UI.TextFieldControl.OnFieldBlurred = () =>
            {
                RunOnUiThread(() =>
                {
                    HideKeyboard();
                });
            };
        }

        private void HideKeyboard()
        {
            try
            {
                var imm = (InputMethodManager)GetSystemService(InputMethodService);
                if (_hiddenInput != null)
                {
                    imm?.HideSoftInputFromWindow(_hiddenInput.WindowToken, 0);
                    _hiddenInput.ClearFocus();
                }
                _activeField = null;
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("MuAndroid", $"Error hiding keyboard: {ex}");
            }
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus && Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
            {
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutHideNavigation |
                    SystemUiFlags.LayoutFullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.Fullscreen |
                    SystemUiFlags.ImmersiveSticky);
            }
        }

        public override void OnRequestPermissionsResult(int req, string[] p, Permission[] res)
            => base.OnRequestPermissionsResult(req, p, res);
    }
}