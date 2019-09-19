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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using JuvoLogger;

namespace JuvoPlayer.Utils
{
    public class PriorityJobChannel
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public interface IJob
        {
            /// <summary>
            /// Job priority level. Lower value, higher execution priority
            /// </summary>
            /// <remarks>
            /// maxPriorities > Priority >= 0
            /// </remarks>
            uint Priority { get; }
        }

        private readonly Dictionary<Type, Action<IJob>> jobHandlers = new Dictionary<Type, Action<IJob>>();

        // TODO:
        // When ChannelReader.ReadAllAsync()
        // https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelreader-1.readallasync
        // will be available, change implementation from multi channels to single channel with priority selection
        // based on returned enumerator.
        //
        private Channel<IJob>[] jobChannels;
        private ChannelWriter<IJob>[] jobWriters;
        private readonly uint maxChannels;
        private Task jobQueueTask;

        public bool IsRunning() => jobQueueTask != null;

        public PriorityJobChannel(uint maxPriorities)
        {
            maxChannels = maxPriorities;
            jobChannels = new Channel<IJob>[maxChannels];
            jobWriters = new ChannelWriter<IJob>[maxChannels];
        }

        private static Task GetChannelReadWaitTask(ChannelReader<IJob> reader) =>
            reader.WaitToReadAsync().AsTask();

        private static Task WaitForJobs(ChannelReader<IJob>[] jobReaders)
        {
            var waitPool = jobReaders.Where(reader => !reader.WaitToReadAsync().IsCompleted)
                .Select(GetChannelReadWaitTask).ToArray();

            return waitPool.Length != jobReaders.Length ? Task.CompletedTask : Task.WhenAny(waitPool);
        }

        private static bool IsQueueRunning(ChannelReader<IJob>[] jobReaders)
        {
            foreach (var reader in jobReaders)
            {
                if (reader.Completion.IsCompleted)
                    return false;
            }

            return true;
        }

        private async ValueTask JobListener()
        {
            Logger.Info("Started");
            var jobReader = new ChannelReader<IJob>[maxChannels];

            for (var channel = 0; channel < maxChannels; channel++)
                jobReader[channel] = jobChannels[channel].Reader;

            try
            {
                do
                {
                    // Priority based round robin with recheck of jobs at higher priority
                    // on successful read of lower priority job
                    for (var channel = 0; channel < maxChannels;)
                    {
                        if (jobReader[channel].TryRead(out var job))
                        {
                            jobHandlers[job.GetType()](job);
                            channel = 0;
                            continue;
                        }

                        channel++;
                    }

                    // No jobs in any channel. Wait for goodies to arrive.                    
                    await WaitForJobs(jobReader);
                } while (IsQueueRunning(jobReader));
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }

            Logger.Info("Finished");
        }

        public void RegisterJobHandler<T>(Action<T> jobHandler)
        {
            void RunJob(IJob job) => jobHandler((T)job);
            jobHandlers.Add(typeof(T), RunJob);
        }

        public bool EnqueueJob(IJob job) =>
            jobWriters[job.Priority].TryWrite(job);

        public async ValueTask EnqueueJobAsync(IJob job) =>
            await jobWriters[job.Priority].WriteAsync(job);

        public Task<Task> Start()
        {
            if (jobQueueTask != null)
                throw new InvalidOperationException("Already running");

            for (var i = 0; i < maxChannels; i++)
            {
                jobChannels[i] = Channel.CreateUnbounded<IJob>(new UnboundedChannelOptions { SingleReader = true });
                jobWriters[i] = jobChannels[i].Writer;
            }

            var startProcess = Task.Factory.StartNew(async () => await JobListener(), TaskCreationOptions.LongRunning);
            jobQueueTask = startProcess.Unwrap();

            return startProcess;
        }

        public void Stop()
        {
            if (jobQueueTask == null)
                throw new InvalidOperationException("Not started");

            for (var i = 0; i < maxChannels; i++)
            {
                jobWriters[i].Complete();
            }

            jobQueueTask = null;
        }
    }
}