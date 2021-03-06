/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;

namespace JuvoPlayer.Drms
{
    public class DrmManager : IDrmManager
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly List<IDrmHandler> drmHandlers = new List<IDrmHandler>();
        private readonly List<DRMDescription> clipDrmConfiguration = new List<DRMDescription>();
        private readonly DrmSessionCache _sessionCache = new DrmSessionCache();

        public void UpdateDrmConfiguration(DRMDescription drmDescription)
        {
            Logger.Info("");

            lock (clipDrmConfiguration)
            {
                var currentDescription = clipDrmConfiguration.FirstOrDefault(o => SchemeEquals(o.Scheme, drmDescription.Scheme));
                if (currentDescription == null)
                {
                    clipDrmConfiguration.Add(drmDescription);
                    return;
                }

                if (currentDescription.IsImmutable)
                {
                    Logger.Warn($"{currentDescription.Scheme} is immutable - ignoring update request");
                    return;
                }

                if (drmDescription.KeyRequestProperties != null)
                    currentDescription.KeyRequestProperties = drmDescription.KeyRequestProperties;
                if (drmDescription.LicenceUrl != null)
                    currentDescription.LicenceUrl = drmDescription.LicenceUrl;
            }
        }

        public void ClearCache()
        {
            Logger.Info("");
            _sessionCache.Clear();
        }

        public void RegisterDrmHandler(IDrmHandler handler)
        {
            lock (drmHandlers)
            {
                drmHandlers.Add(handler);
            }
        }

        public IDrmSession CreateDRMSession(DRMInitData data)
        {
            Logger.Info("Create DrmSession");

            // Before diving into locks, decode DRM InitData KeyIDs
            var keyIds = DrmInitDataTools.GetKeyIds(data);
            var useGenericKey = keyIds.Count == 0;
            if (useGenericKey)
            {
                Logger.Info("No keys found. Using entire DRMInitData.InitData as generic key");
                keyIds.Add(data.InitData);
            }
            else
            {
                // Early exit scenario - already cached
                if (_sessionCache.TryGetSession(keyIds, out IDrmSession session))
                {
                    Logger.Info("Cached session found");
                    return session;
                }
            }

            lock (drmHandlers)
            {
                var handler = drmHandlers.FirstOrDefault(o => o.SupportsSystemId(data.SystemId));
                if (handler == null)
                {
                    Logger.Warn("unknown drm init data");
                    return null;
                }

                var scheme = handler.GetScheme(data.SystemId);

                lock (clipDrmConfiguration)
                {
                    var drmConfiguration = clipDrmConfiguration.FirstOrDefault(o => SchemeEquals(o.Scheme, scheme));
                    if (drmConfiguration == null)
                    {
                        Logger.Warn("drm not configured");
                        return null;
                    }

                    // Recheck needs to be done for cached session.
                    // Early check may produce false negatives - session being created by other stream
                    // but not yet cached.
                    if (_sessionCache.TryGetSession(keyIds, out IDrmSession session))
                    {
                        Logger.Info("Cached session found");
                        return session;
                    }

                    Logger.Info("No cached session found");

                    session = handler.CreateDRMSession(data, drmConfiguration);
                    session.Share();
                    if (_sessionCache.TryAddSession(keyIds, session))
                        return session;

                    Logger.Info("Failed to cache session");
                    session.Release();

                    return session;
                }
            }
        }

        private static bool SchemeEquals(string scheme1, string scheme2)
        {
            return string.Equals(scheme1, scheme2, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
