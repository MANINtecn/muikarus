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

            Client.Main.Controls.UI.TextFieldControl.ShowKeyboardAsync = (title, desc, defText, isPassword) =>
            {
                return ShowTextInputDialogAsync(title, desc, defText, isPassword);
            };

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

        private Task<string> ShowTextInputDialogAsync(string title, string description, string defaultText, bool usePasswordMode)
        {
            var tcs = new TaskCompletionSource<string>();

            RunOnUiThread(() =>
            {
                try
                {
                    var container = new FrameLayout(this);
                    var lp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                    int pad = (int)(20 * Resources.DisplayMetrics.Density);
                    lp.SetMargins(pad, pad / 2, pad, pad / 2);

                    var input = new EditText(this);
                    input.LayoutParameters = lp;
                    input.Text = defaultText ?? string.Empty;
                    input.SetSingleLine(true);
                    input.TextSize = 16f;

                    if (usePasswordMode)
                    {
                        input.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;
                        input.TransformationMethod = Android.Text.Method.PasswordTransformationMethod.Instance;
                        input.ImeOptions = ImeAction.Done;
                    }
                    else
                    {
                        input.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationVisiblePassword;
                        input.ImeOptions = ImeAction.Next;
                    }

                    if (!string.IsNullOrEmpty(defaultText))
                    {
                        input.SetSelection(input.Text.Length);
                    }

                    container.AddView(input);

                    AlertDialog dialog = null;

                    var builder = new AlertDialog.Builder(this, Android.Resource.Style.ThemeDeviceDefaultDialogAlert)
                        .SetTitle(title)
                        .SetView(container)
                        .SetPositiveButton("OK", (sender, args) =>
                        {
                            tcs.TrySetResult(input.Text);
                        })
                        .SetNegativeButton("Cancelar", (sender, args) =>
                        {
                            tcs.TrySetResult(null);
                        })
                        .SetCancelable(true);

                    if (!string.IsNullOrEmpty(description))
                    {
                        builder.SetMessage(description);
                    }

                    dialog = builder.Create();
                    dialog.DismissEvent += (sender, args) =>
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(null);
                        }
                    };

                    input.EditorAction += (s, e) =>
                    {
                        if (e.ActionId == ImeAction.Next || e.ActionId == ImeAction.Done || e.ActionId == ImeAction.Go || e.Event?.KeyCode == Keycode.Enter)
                        {
                            tcs.TrySetResult(input.Text);
                            dialog?.Dismiss();
                        }
                    };

                    dialog.Window?.SetSoftInputMode(SoftInput.StateAlwaysVisible);
                    dialog.Show();

                    input.RequestFocus();
                    input.PostDelayed(() =>
                    {
                        var imm = (InputMethodManager)GetSystemService(InputMethodService);
                        imm?.ShowSoftInput(input, ShowFlags.Forced);
                    }, 100);
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("MuAndroid", $"Error showing text dialog: {ex}");
                    tcs.TrySetResult(null);
                }
            });

            return tcs.Task;
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