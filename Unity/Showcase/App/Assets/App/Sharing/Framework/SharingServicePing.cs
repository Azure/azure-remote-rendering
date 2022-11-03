// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public struct LapTimer
    {
        private Stopwatch stopWatch;

        private List<TimeSpan> timestamps;

        public int LapCount => timestamps.Count;

        public static LapTimer Create()
        {
            LapTimer timer = new LapTimer{ 
                timestamps = new List<TimeSpan>(),
                stopWatch = new Stopwatch() };

            timer.Start();

            return timer;
        }

        public void Start()
        {
            timestamps.Clear();

            stopWatch.Start();
        }

        public void Stop()
        {
            stopWatch.Stop();
            timestamps.Add(stopWatch.Elapsed);
        }

        public void Lap()
        {
            timestamps.Add(stopWatch.Elapsed);
        }

        public TimeSpan GetCurrentTime()
        {
            return stopWatch.Elapsed;
        }

        public TimeSpan GetLapTime(int index = 0)
        {
            TimeSpan timeSpan;

            if (index < timestamps.Count)
            {
                return timestamps[index];
            }

            return timeSpan;
        }
    }

    /// <summary>
    /// Message to be sent over the sharing service
    /// </summary>
    [Serializable]
    public struct SharingServicePingRequest
    {
        public byte Id;

        private LapTimer lapTimer;

        public static SharingServicePingRequest Create(byte id)
        {
            return new SharingServicePingRequest()
            {
                Id = id,
                lapTimer = LapTimer.Create()
            };
        }

        public void Lap()
        {
            lapTimer.Lap();
        }

        public TimeSpan GetDelta(ref SharingServicePingResponse response)
        {
            if (response.Id != Id)
            {
                return TimeSpan.Zero;
            }

            return lapTimer.GetCurrentTime() - response.GetElapsedTime() - lapTimer.GetLapTime(0);
        }
    }

    [Serializable]
    public struct SharingServicePingResponse
    {
        public byte Id;

        public LapTimer lapTimer;

        public static SharingServicePingResponse Create(byte id)
        {
            return new SharingServicePingResponse()
            {
                Id = id,
                lapTimer = LapTimer.Create()
            };
        }

        internal TimeSpan GetElapsedTime()
        {
            lapTimer.Stop();

            return lapTimer.GetCurrentTime();
        }
    }

    public struct SharingServicePingRequestSerializer : ISharingServiceSerializer
    {
        public int GetByteSize(object value)
        {
            if (!(value is SharingServicePingRequest))
            {
                return 0;
            }

            // id
            return 1;
        }

        public void Serialize(object value, byte[] target, ref int offset)
        {
            if (!(value is SharingServicePingRequest))
            {
                throw new InvalidCastException();
            }

            // make sure there is enough room for the amount of data that is required for object
            if (target.Length < (offset + 2))
            {
                throw new ArgumentOutOfRangeException();
            }

            SharingServicePingRequest request = (SharingServicePingRequest)value;
            target[offset++] = 1;
            target[offset++] = request.Id;

            // set stopwatch time
            request.Lap();
        }

        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            byte sizeOfObject = source[offset++];

            if (sizeOfObject != 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            SharingServicePingRequest request = SharingServicePingRequest.Create(source[offset++]);

            value = request;
        }

        public string ToString(object value)
        {
            throw new NotImplementedException();
        }

        public bool FromString(string value, out object result)
        {
            throw new NotImplementedException();
        }
    }

    public struct SharingServicePingResponseSerializer : ISharingServiceSerializer
    {
        public int GetByteSize(object value)
        {
            if (!(value is SharingServicePingResponse))
            {
                return 0;
            }

            // id
            return 1;
        }

        public void Serialize(object value, byte[] target, ref int offset)
        {
            if (!(value is SharingServicePingResponse))
            {
                throw new InvalidCastException();
            }

            // make sure there is enough room for the amount of data that is required for object
            if (target.Length < (offset + 2))
            {
                throw new ArgumentOutOfRangeException();
            }

            SharingServicePingResponse response = (SharingServicePingResponse)value;
            target[offset++] = 1;
            target[offset++] = response.Id;
        }

        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            if (source[offset++] != 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            value = SharingServicePingResponse.Create(source[offset++]);
        }

        public string ToString(object value)
        {
            throw new NotImplementedException();
        }

        public bool FromString(string value, out object result)
        {
            throw new NotImplementedException();
        }
    }
}
