﻿/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer.Stream.Buffering;
using Nito.AsyncEx;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    public class TSStreamSynchronizer
    {
        private async Task TransferTaskSychStreams(StreamType stream, StreamBufferController streamBuffer, CancellationToken token,
            AsyncLock alock, Action addSync)
        {
            var rnd = new Random((int)DateTime.Now.Ticks);
            var packet = new Packet
            {
                Storage = new dummyStorage(),
                StreamType = stream
            };
            var streamSynchronizer = streamBuffer.StreamSynchronizer;

            try
            {
                for (; ; )
                {

                    using (await alock.LockAsync(token))
                    {
                        for (; ; )
                        {
                            streamBuffer.DataOut(packet);
                            packet.Dts += TimeSpan.FromMilliseconds(rnd.Next(50, 150));
                            streamSynchronizer.UpdateSynchronizationTraps(stream);

                            if (streamSynchronizer.IsStreamSyncNeeded(stream))
                                break;
                        }
                    }

                    await streamSynchronizer.SynchronizeStreams(stream, token);
                    addSync();
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                packet.Dispose();
            }
        }

        [Test]
        public void SynchronizeWithStreams()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var cts = new CancellationTokenSource();
                        var token = cts.Token;
                        var avlock = new AsyncLock();


                        var audioSync = 0;
                        var videoSync = 0;

                        var audioTask = Task.Factory.StartNew(async () =>
                            await TransferTaskSychStreams(StreamType.Audio, bufferController, token, avlock,
                                () => { audioSync++; })).Unwrap();

                        var videoTask = Task.Factory.StartNew(async () =>
                            await TransferTaskSychStreams(StreamType.Video, bufferController, token, avlock,
                                () => { videoSync++; })).Unwrap();

                        SpinWait.SpinUntil(() => audioSync >= 3 && videoSync >= 3, TimeSpan.FromSeconds(10));

                        cts.Cancel();

                        Assert.IsTrue(audioSync > 0);
                        Assert.IsTrue(videoSync > 0);
                    }

                });
        }

        private async Task TransferTaskSychClock(StreamType stream, StreamBufferController streamBuffer,
            CancellationToken token, Action addSync)
        {
            var rnd = new Random((int)DateTime.Now.Ticks);
            var packet = new Packet
            {
                Storage = new dummyStorage(),
                StreamType = stream
            };
            var streamSynchronizer = streamBuffer.StreamSynchronizer;

            try
            {
                for (; ; )
                {

                    streamBuffer.DataOut(packet);
                    packet.Dts += TimeSpan.FromMilliseconds(rnd.Next(50, 150));
                    streamSynchronizer.UpdateSynchronizationTraps(stream);

                    if (!streamSynchronizer.IsPlayerClockSyncNeeded(stream))
                        continue;

                    await streamSynchronizer.SynchronizePlayerClock(stream, token);
                    addSync();

                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                packet.Dispose();
            }
        }

        [Test]
        public void SynchronizeWithPlayerClock()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        var clockStart = DateTime.Now;
                        bufferController.StreamSynchronizer.PlayerClock = () => DateTime.Now - clockStart;
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var cts = new CancellationTokenSource();
                        var token = cts.Token;

                        var audioSync = 0;
                        var videoSync = 0;

                        var audioTask = Task.Factory.StartNew(async () =>
                            await TransferTaskSychClock(StreamType.Audio, bufferController, token,
                                () => { audioSync++; })).Unwrap();

                        var videoTask = Task.Factory.StartNew(async () =>
                            await TransferTaskSychClock(StreamType.Video, bufferController, token,
                                () => { videoSync++; })).Unwrap();

                        SpinWait.SpinUntil(() => audioSync >= 3 && videoSync >= 3, TimeSpan.FromSeconds(10));

                        cts.Cancel();

                        Assert.IsTrue(audioSync > 0);
                        Assert.IsTrue(videoSync > 0);
                    }
                });
        }
    }
}
