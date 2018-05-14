﻿using System;
using System.IO;
using JuvoLogger;
using JuvoPlayer.OpenGL.Services;
using Tizen;
using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
        private const bool LoadTestContentList = true;

        private readonly TimeSpan _prograssBarFadeout = TimeSpan.FromMilliseconds(5000);
        private readonly TimeSpan _defaultSeekTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _defaultSeekAccumulateTime = TimeSpan.FromMilliseconds(1000);

        private DateTime _lastAction;
        private int _selectedTile;
        private bool _menuShown;
        private bool _progressBarShown;
        private bool _metricsShown;

        private PlayerService _player;
        private int _playerTimeCurrentPosition;
        private int _playerTimeDuration;
        private int _playerState;
        private bool _handlePlaybackCompleted;

        private ILogger Logger;

        private OptionsMenu _options;
        private ResourceLoader _resourceLoader;

        protected override void OnCreate()
        {
            DllImports.Create();
            InitMenu();
        }

        private void InitMenu()
        {
            _resourceLoader = new ResourceLoader
            {
                Logger = Logger
            };
            _resourceLoader.LoadResources(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), LoadTestContentList);
            SetMenuFooter();
            SetupLogger();
            SetupOptionsMenu();
            SetDefaultMenuState();
        }

        private void SetMenuFooter()
        {
            var footer = "JuvoPlayer Prealpha, OpenGL UI #" + DllImports.OpenGLLibVersion().ToString("x") +
                            ", Samsung R&D Poland 2017-2018";
            fixed (byte* f = ResourceLoader.GetBytes(footer))
                DllImports.SetFooter(f, footer.Length);
        }

        private void SetDefaultMenuState()
        {
            DllImports.SelectTile(_selectedTile);
            _selectedTile = 0;
            _menuShown = true;
            DllImports.ShowLoader(1, 0);

            _lastAction = DateTime.Now;
            _accumulatedSeekTime = TimeSpan.Zero;
            _lastSeekTime = DateTime.MinValue;

            _playerTimeCurrentPosition = 0;
            _playerTimeDuration = 0;
            _playerState = (int)PlayerState.Idle;
            _handlePlaybackCompleted = false;

            _metricsShown = false;
        }

        private void SetupLogger()
        {
            Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        }

        private void SetupOptionsMenu()
        {
            _options = new OptionsMenu
            {
                Logger = Logger
            };
        }

        protected override void OnKeyEvent(Key key)
        {
            if (key.State != Key.StateType.Down)
                return;

            switch (key.KeyPressedName)
            {
                case "Right":
                    HandleKeyRight();
                    break;
                case "Left":
                    HandleKeyLeft();
                    break;
                case "Up":
                    HandleKeyUp();
                    break;
                case "Down":
                    HandleKeyDown();
                    break;
                case "Return":
                    HandleKeyReturn();
                    break;
                case "XF86Back":
                    HandleKeyBack();
                    break;
                case "XF86Exit":
                    HandleKeyExit();
                    break;
                case "XF863XSpeed":
                    HandleKeyPlay();
                    break;
                case "XF86AudioPause":
                    HandleKeyPause();
                    break;
                case "XF863D":
                    HandleKeyStop();
                    break;
                case "XF86AudioRewind":
                    HandleKeyRewind();
                    break;
                case "XF86AudioNext":
                    HandleKeySeekForward();
                    break;
                case "XF86Info":
                    break;
                case "XF86Red":
                    _metricsShown = !_metricsShown;
                    DllImports.SetGraphVisibility(DllImports.fpsGraphId, _metricsShown ? 1 : 0);
                    break;
                case "XF86Green":
                    _menuShown = !_menuShown;
                    DllImports.ShowMenu(_menuShown ? 1 : 0);
                    break;
                case "XF86Yellow":
                    DllImports.SwitchTextRenderingMode();
                    break;
                case "XF86Blue":
                    break;
                default:
                    Logger?.Info("Unknown key pressed: " + key.KeyPressedName);
                    break;
            }

            KeyPressedMenuUpdate();
        }

        private void KeyPressedMenuUpdate()
        {
            _lastAction = DateTime.Now;
            _progressBarShown = !_menuShown;
            if (!_progressBarShown && _options.IsShown())
                _options.Hide();
        }

        protected override void OnUpdate(IntPtr eglDisplay, IntPtr eglSurface)
        {
            _resourceLoader.LoadQueuedResources();
            UpdateUI();
            DllImports.Draw(eglDisplay, eglSurface);
        }

        private static void Main(string[] args)
        {
            var myProgram = new Program();
            myProgram.Run(args);
        }
    }
}