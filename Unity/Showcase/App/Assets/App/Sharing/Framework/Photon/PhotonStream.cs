// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonStream : Stream
    {
        private StreamBuffer _inner;

        #region Constructor
        private PhotonStream(StreamBuffer inner, bool read, bool write, int length = -1)
        {
            _inner = inner;
            CanRead = read;
            CanWrite = write;

            if (length < 0 || length > _inner.Length)
            {
                Length = _inner.Length;
            }
            else
            {
                Length = length;
            }
        }
        #endregion Constructors

        #region Public Properties
        public override bool CanRead { get; }

        public override bool CanSeek => true;

        public override bool CanWrite { get; }

        public override long Length { get; }

        public override long Position
        {
            get => _inner.Position;

            set
            {
                if (value > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException();
                }

                _inner.Position = (int)value;
            }
        }
        #endregion Public Properties

        #region Static Methods
        public static PhotonStream CreateReader(StreamBuffer buffer, int length = -1)
        {
            return new PhotonStream(buffer, read: true, write: false, length);
        }

        public static PhotonStream CreateWriter(StreamBuffer buffer, int length = -1)
        {
            return new PhotonStream(buffer, read: false, write: true, length);
        }
        #endregion Static Methods

        #region Public Methods
        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
        }

        public override int ReadByte()
        {
            return _inner.ReadByte();
        }

        public override void WriteByte(byte value)
        {
            _inner.WriteByte(value);
        }
        #endregion Public Methods
    }
}
#endif // PHOTON_INSTALLED
