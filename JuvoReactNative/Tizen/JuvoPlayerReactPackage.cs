﻿using System;
using System.Collections.Generic;

using ReactNative;
using ReactNative.Common;
using ReactNative.Tracing;
using ReactNative.Shell;
using ReactNative.Modules.Core;
using ReactNative.Bridge;
using ReactNative.Collections;

using JuvoPlayer;
using JuvoLogger;
using JuvoLogger.Tizen;
using ILogger = JuvoLogger.ILogger;
using Log = Tizen.Log;

using Tizen.Applications;
using Tizen;
using ReactNative.UIManager;

namespace JuvoReactNative
{
    public class JuvoPlayerReactPackage : IReactPackage
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public static readonly string Tag = "JuvoRN";

        public IReadOnlyList<INativeModule> CreateNativeModules(ReactContext reactContext)
        {
            Log.Error(Tag, "JuvoPlayerReactPackage CreateNativeModules called! ");
            return new List<INativeModule>
            {
                new JuvoPlayerModule(reactContext)
            };
        }
        public IReadOnlyList<Type> CreateJavaScriptModulesConfig()
        {
            return new List<Type>(0);
        }
        public IReadOnlyList<IViewManager> CreateViewManagers(
            ReactContext reactContext)
        {
            return new List<IViewManager>(0);
        }
    }
}