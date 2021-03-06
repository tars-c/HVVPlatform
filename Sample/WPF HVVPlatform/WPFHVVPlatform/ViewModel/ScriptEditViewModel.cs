﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DevExpress.Xpf.CodeView;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using WPFHVVPlatform.Model;
using WPFHVVPlatform.Service;

namespace WPFHVVPlatform.ViewModel
{
    public class ScriptEditViewModel : ViewModelBase
    {

        private readonly FileDialogService fileDialogService;
        private readonly MessageDialogService messageDialogService;
        private readonly ScriptFileService scriptFileService;
        private readonly HV.V1.Interpreter interpreter;
        private readonly AppConfigService appConfigService;


        private string _trackingName;
        private string _trackingType;
        private bool _isTracking = false;


        public ScriptEditViewModel(FileDialogService _fileDialogService,
                                   MessageDialogService _messageDialogService,
                                   ScriptFileService _scriptFileService,
                                   AppConfigService _appConfigService,
                                   HV.V1.Interpreter _interpreter)
        {

            this.fileDialogService = _fileDialogService;
            this.messageDialogService = _messageDialogService;
            this.scriptFileService = _scriptFileService;
            this.appConfigService = _appConfigService;


            this.interpreter = _interpreter;
            this.interpreter.TraceEvent += Trace;



            MessengerInstance.Register<NotificationMessage>(this, NotifyMessage);
        }

        ~ScriptEditViewModel()
        {
            this.interpreter.TraceEvent -= Trace;
        }


        public void NotifyMessage(NotificationMessage message)
        {
            if(message.Notification == "ClearNativeModules")
            {

                this.GlobalCollection.Clear();
                
                this.SelectedGlobal = null;
                this.DetailImageDrawCollection.Clear();
                this.NativeModuleCollection.Clear();

                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.WaitForFullGCComplete();

                this.interpreter.ReleaseNativeModules();

                if(this.interpreter.NativeModules.Count() > 0)
                    this.NativeModuleCollection.AddRange(this.interpreter.NativeModules.Values.ToList());
             }
        }


        private void Trace(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.LogCollection.Insert(0,new Log()
                {
                    Type = "스크립트",
                    Content = text
                });
            });
            Thread.Sleep(1);
        }

        private WriteableBitmap _TrackingImagePresenter = null;
        public WriteableBitmap TrackingImagePresenter
        {
            set => Set<WriteableBitmap>(nameof(TrackingImagePresenter), ref _TrackingImagePresenter, value);
            get => _TrackingImagePresenter;
        }

        private WriteableBitmap _DetailImagePresenter = null;
        public WriteableBitmap DetailImagePresenter
        {
            set => Set<WriteableBitmap>(nameof(DetailImagePresenter), ref _DetailImagePresenter, value);
            get => _DetailImagePresenter;
        }


        private Script _SelectedScript = null;
        public Script SelectedScript
        {
            get => _SelectedScript;
            set => Set<Script>(nameof(SelectedScript), ref _SelectedScript, value);
        }


        public ICommand OpenScriptFileCommand
        {
            get => new RelayCommand(() =>
             {
                 var filePath = this.fileDialogService.OpenFile("Script File (.js)|*.js");
                 if (filePath.Length == 0) return;
                 var content = this.scriptFileService.LoadScriptFile(filePath);
                 var script = new Script()
                 {
                     FilePath = filePath,
                     FileName = Path.GetFileName(filePath),
                     ScriptContent = content
                 };

                 this.ScriptCollection.Add(script);
                 this.SelectedScript = script;

             });
            
        }

        public ICommand NewScriptFileCommand
        {
            get => new RelayCommand(() =>
            {
                this.ScriptCollection.Add(new Script()
                {
                    FileName = "new.js",
                    ScriptContent = "/* Be the god of coding */",
                    FilePath = ""
                });

                //System.Console.WriteLine("test");
            });
        }

        public ICommand SaveScriptFileCommand
        {
            get => new RelayCommand(() =>
            {
                if (this.SelectedScript == null)
                    return;

                if(this.SelectedScript.FilePath.Length == 0)
                {
                    var filePath = this.fileDialogService.SaveFile("Script File (.js)|*.js");
                    if (filePath.Length == 0) return;
                    this.SelectedScript.FilePath = filePath;
                    this.SelectedScript.FileName = Path.GetFileName(filePath);
                    RaisePropertyChanged(nameof(SelectedScript));
                }

                this.scriptFileService.SaveScriptFile(this.SelectedScript.FilePath, this.SelectedScript.ScriptContent);
            });
        }

        public ICommand OpenImageCommand
        {
            get => new RelayCommand(() =>
            {

            });
        }

        public ICommand StartRunScriptCommand
        {
            get => new RelayCommand(async () =>
            {
                if (this.IsRunningScript == true) return;
                if (this.SelectedScript == null) return;

                this.interpreter.SetModulePath(this.appConfigService.ApplicationSetting.ModuleMainPath);

                await Task.Run(async () =>
                {
               
                    this.IsRunningScript = true;
                    try
                    {
                        this.interpreter.RunScript(this.SelectedScript.ScriptContent);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.GlobalCollection.Clear();
                            this.GlobalCollection.AddRange(this.interpreter.GlobalObjects.Values.ToList());

                            this.NativeModuleCollection.Clear();
                            this.NativeModuleCollection.AddRange(this.interpreter.NativeModules.Values.ToList());

                            if (this._isTracking == false) return;
                            try
                            {
                                var image = this.interpreter.GlobalObjects.Values.ToList().Where((_object) =>
                                {
                                    return _object.Name.Contains(this._trackingName) && _object.Type.Contains(this._trackingType);
                                }).First();

                                var hvImage = new HV.V1.Image(image);
                                var width = hvImage.Width;
                                var height = hvImage.Height;
                                var stride = hvImage.Stride;
                                var size = hvImage.Size;
                                if (TrackingImagePresenter == null || TrackingImagePresenter.Width != width || TrackingImagePresenter.Height != height)
                                {
                                    TrackingImagePresenter = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
                                }
                                TrackingImagePresenter.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), hvImage.Ptr(), size, stride);
                            }
                            catch (Exception e)
                            {

                            }
                        }, DispatcherPriority.Send);
                    }
                    catch (HV.V1.ScriptError e)
                    {
                        System.Console.WriteLine(e.Message);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            string errorContent = string.Format("Error Line:{0}, Column({1},{2})\n{3}", e.Line(), e.StartColumn(), e.EndColumn(), e.Message);
                            this.LogCollection.Add(new Log()
                            {
                                Type = "Error",
                                Content = errorContent
                            });

                            messageDialogService.ShowToastErrorMessage("스크립트 에러메세지", errorContent);
                        }, DispatcherPriority.Send);
                        
                    }

                    this.IsRunningScript = false;
                });
            });
        }

        public ICommand ContinusStartRunScriptCommand
        {
            get => new RelayCommand(() =>
            {
                if (this.IsRunningScript == true) return;
                if (this.SelectedScript == null) return;

                this.IsRunningScript = true;


                this.interpreter.SetModulePath(this.appConfigService.ApplicationSetting.ModuleMainPath);


                Task.Run(async () =>
                {
                    var count = 0;
                    var stacked_time = 0.0;

                    while (IsRunningScript)
                    {
                        await Task.Delay(10);
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                           
                            this.interpreter.RunScript(this.SelectedScript.ScriptContent);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                this.GlobalCollection.Clear();
                                this.GlobalCollection.AddRange(this.interpreter.GlobalObjects.Values.ToList());

                                this.NativeModuleCollection.Clear();
                                this.NativeModuleCollection.AddRange(this.interpreter.NativeModules.Values.ToList());

                                if (this._isTracking == false) return;
                                try
                                {
                                    var image = this.interpreter.GlobalObjects.Values.ToList().Where((_object) =>
                                    {
                                        return _object.Name.Contains(this._trackingName) && _object.Type.Contains(this._trackingType);
                                    }).First();

                                    var hvImage = new HV.V1.Image(image);
                                    var width = hvImage.Width;
                                    var height = hvImage.Height;
                                    var stride = hvImage.Stride;
                                    var size = hvImage.Size;
                                    if (TrackingImagePresenter == null || TrackingImagePresenter.Width != width || TrackingImagePresenter.Height != height)
                                    {
                                        TrackingImagePresenter = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
                                        TrackingImagePresenter.Freeze();
                                    }
                                    TrackingImagePresenter.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), hvImage.Ptr(), size, stride);
                                }
                                catch (Exception e)
                                {

                                }
                            }, DispatcherPriority.Send);
                        }
                        catch (HV.V1.ScriptError e)
                        {

                            System.Console.WriteLine(e.Message);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                string errorContent = string.Format("Error Line:{0}, Column({1},{2})\n{3}", e.Line(), e.StartColumn(), e.EndColumn(), e.Message);
                                this.LogCollection.Add(new Log()
                                {
                                    Type = "Error",
                                    Content = errorContent
                                });

                                messageDialogService.ShowToastErrorMessage("스크립트 에러메세지", errorContent);
                            }, DispatcherPriority.Send);

                            break;
                        }
                        
                        watch.Stop();
                        var elapsedMs = watch.ElapsedMilliseconds;
                        stacked_time += elapsedMs;
                        count++;
                        if(stacked_time > 1000)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                this.CurrentFPS = count.ToString("F2");
                                this.CurrentExecutionTime = elapsedMs.ToString() + " ms";
                            }, DispatcherPriority.Send);
                            count = 0;
                            stacked_time = 0;
                        }

       
                    }

                    this.IsRunningScript = false;
                });

            });
        }

        public ICommand StopScriptCommand
        {
            get => new RelayCommand(() =>
            {
                this.interpreter.Terminate();
                this.IsRunningScript = false;
            });
        }

        public ICommand TrackingImageCommand
        {
            get => new RelayCommand(() =>
            {
                if (this.SelectedGlobal == null) return;
                if (!this.SelectedGlobal.Type.Contains("image")) return;

                this._trackingType = this.SelectedGlobal.Type;
                this._trackingName = this.SelectedGlobal.Name;
                this._isTracking = true;

                var hvImage = new HV.V1.Image(this.SelectedGlobal);
                if (TrackingImagePresenter == null || TrackingImagePresenter.Width != hvImage.Width || TrackingImagePresenter.Height != hvImage.Height)
                {
                    TrackingImagePresenter = new WriteableBitmap(hvImage.Width, hvImage.Height, 96, 96, PixelFormats.Gray8, null);
                }
                TrackingImagePresenter.WritePixels(new System.Windows.Int32Rect(0, 0, hvImage.Width, hvImage.Height), hvImage.Ptr(), hvImage.Size, hvImage.Stride);
            });
        }

        public ICommand ReleaseTrackingImageCommand
        {
            get => new RelayCommand(() =>
            {
                this._isTracking = false;
            });
        }

        public ICommand DetailImageShowCommand
        {
            get => new RelayCommand(() =>
            {
                if (this.SelectedGlobal == null) return;
                if (!this.SelectedGlobal.Type.Contains("image")) return;

                var hvImage = new HV.V1.Image(this.SelectedGlobal);
                if (DetailImagePresenter == null || DetailImagePresenter.Width != hvImage.Width || DetailImagePresenter.Height != hvImage.Height)
                {
                    DetailImagePresenter = new WriteableBitmap(hvImage.Width, hvImage.Height, 96, 96, PixelFormats.Gray8, null);
                }
                DetailImagePresenter.WritePixels(new System.Windows.Int32Rect(0, 0, hvImage.Width, hvImage.Height), hvImage.Ptr(), hvImage.Size, hvImage.Stride);
                this.DetailImageDrawCollection = new ObservableCollection<HV.V1.Object>(hvImage.DrawObjects);
            });
        }

        private ObservableCollection<Script> _ScriptCollection = null;
        public ObservableCollection<Script> ScriptCollection
        {
            get
            {
                if(_ScriptCollection == null)
                {
                    _ScriptCollection = new ObservableCollection<Script>();
                }
                return _ScriptCollection;
            }
        }

        private ObservableCollection<HV.V1.Object> _GlobalCollection = null;
        public ObservableCollection<HV.V1.Object> GlobalCollection
        {
            get
            {
                if (_GlobalCollection == null)
                {
                    _GlobalCollection = new ObservableCollection<HV.V1.Object>();
                }
                return _GlobalCollection;
            }
            set => Set<ObservableCollection<HV.V1.Object>>(nameof(GlobalCollection), ref _GlobalCollection, value);
        }

        private ObservableCollection<HV.V1.Object> _DetailImageDrawCollection = null;
        public ObservableCollection<HV.V1.Object> DetailImageDrawCollection
        {
            get
            {
                if (_DetailImageDrawCollection == null)
                {
                    _DetailImageDrawCollection = new ObservableCollection<HV.V1.Object>();
                }
                return _DetailImageDrawCollection;
            }
            set => Set<ObservableCollection<HV.V1.Object>>(nameof(DetailImageDrawCollection), ref _DetailImageDrawCollection, value);
        }

        private HV.V1.Object _SelectedGlobal = null;
        public HV.V1.Object SelectedGlobal
        {
            get => _SelectedGlobal;
            set => Set<HV.V1.Object>(nameof(SelectedGlobal), ref _SelectedGlobal, value);
        }

        private bool _IsRunningScript = false;
        public bool IsRunningScript
        {
            get => _IsRunningScript;
            set => Set<bool>(nameof(IsRunningScript), ref _IsRunningScript, value);
        }


        private bool _IsShowingResult = false;
        public bool IsShowingResult
        {
            get => _IsShowingResult;
            set => Set<bool>(nameof(IsShowingResult), ref _IsShowingResult, value);
        }


        private string _CurrentFPS = "";
        public string CurrentFPS
        {
            get => _CurrentFPS;
            set => Set<string>(nameof(CurrentFPS), ref _CurrentFPS, value);
        }

        private string _CurrentExecutionTime = "";
        public string CurrentExecutionTime
        {
            get => _CurrentExecutionTime;
            set => Set<string>(nameof(CurrentExecutionTime), ref _CurrentExecutionTime, value);
        }



        private ObservableCollection<Log> _LogCollection = null;
        public ObservableCollection<Log> LogCollection
        {
            get
            {
                if (_LogCollection == null)
                {
                    _LogCollection = new ObservableCollection<Log>();
                }
                return _LogCollection;
            }
        }


        private ObservableCollection<HV.V1.NativeModule> _NativeModuleCollection = null;
        public ObservableCollection<HV.V1.NativeModule> NativeModuleCollection
        {
            get
            {
                if (_NativeModuleCollection == null)
                {
                    _NativeModuleCollection = new ObservableCollection<HV.V1.NativeModule>();
                }
                return _NativeModuleCollection;
            }
        }

    }
}
