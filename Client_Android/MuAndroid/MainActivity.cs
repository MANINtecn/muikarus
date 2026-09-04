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

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            RequestLegacyWritePermission();

            // Connect TextFieldControl virtual keyboard with native Android dialog
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
                    var input = new EditText(this);
                    input.Text = defaultText ?? string.Empty;
                    if (!string.IsNullOrEmpty(defaultText))
                    {
                        input.SetSelection(input.Text.Length);
                    }

                    if (usePasswordMode)
                    {
                        input.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword;
                        input.TransformationMethod = Android.Text.Method.PasswordTransformationMethod.Instance;
                    }
                    else
                    {
                        input.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationVisiblePassword;
                    }

                    var builder = new AlertDialog.Builder(this)
                        .SetTitle(title)
                        .SetMessage(description)
                        .SetView(input)
                        .SetPositiveButton("OK", (sender, args) =>
                        {
                            tcs.TrySetResult(input.Text);
                        })
                        .SetNegativeButton("Cancelar", (sender, args) =>
                        {
                            tcs.TrySetResult(null);
                        })
                        .SetCancelable(true);

                    var dialog = builder.Create();
                    dialog.DismissEvent += (sender, args) =>
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(null);
                        }
                    };

                    dialog.Window?.SetSoftInputMode(SoftInput.StateAlwaysVisible);
                    dialog.Show();

                    input.RequestFocus();
                    var imm = (InputMethodManager)GetSystemService(InputMethodService);
                    imm?.ShowSoftInput(input, ShowFlags.Implicit);
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