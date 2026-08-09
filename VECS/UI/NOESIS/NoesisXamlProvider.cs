using Noesis;
using NoesisApp;
using System;
using System.Collections.Generic;
using System.IO;

namespace VECS.UI
{
    public class NoesisXamlProvider : LocalXamlProvider
    {
        private readonly string _basePath;

        private readonly Dictionary<string, byte[]> _xamlDataBase = [];

        public NoesisXamlProvider()
            : this("")
        {
        }

        public NoesisXamlProvider(string basePath)
        {
            _basePath = basePath;
        }

        public override Stream LoadXaml(Uri uri)
        {

            string path = System.IO.Path.Combine(_basePath, uri.GetPath());
            if(_xamlDataBase.TryGetValue(path,out var fileBytes))
            {
                return new InternalStream(fileBytes);
            }
            if (File.Exists(path))
            {
                fileBytes = File.ReadAllBytes(path);
                if(!_xamlDataBase.TryAdd(path, fileBytes))
                {
                    fileBytes = _xamlDataBase[path];
                }
                return new InternalStream(fileBytes);
            }

            return null;
        }
    }
    public class InternalStream : Stream
    {
        private readonly byte[] _streamBytes;

        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _streamBytes.LongLength;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public InternalStream(byte[] streamBytes)
        {
            _streamBytes = streamBytes;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset + count >= buffer.Length)
            {
                throw new IndexOutOfRangeException("trying to write to point outside of array");
            }

            if (Position + count > Length)
            {
                count = (int)(Length - Position);
            }

            Array.Copy(_streamBytes, Position, buffer, offset, count);

            Position += count;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length - 1 - offset;
                    break;
            }
            Position = Math.Clamp(Position, 0, Length);

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
