
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VECS.UI
{

    public struct Header
    {
        public uint magic;
        public uint size;
    };

    public static class FastLZ
    {

        private const uint MAX_L2_DISTANCE = 8191;
        private const uint MAGIC = 0x4b50534e;

        public static unsafe uint DecompressBufferSize(void* buffer)
        {
            Header* header = (Header*)buffer;
            Debug.Assert(header->magic == MAGIC);
            return header->size;
        }
        public static unsafe void Decompress(void* input, uint length, void* output)
        {
            Header* header = (Header*)input;
            Debug.Assert(header->magic == MAGIC);
            int maxout = (int)header->size;

            byte* ip = (byte*)input + sizeof(Header);
            byte* ip_limit = ip + length - sizeof(Header);
            byte* ip_bound = ip_limit - 2;
            byte* op = (byte*)output;
            byte* op_limit = op + maxout;
            uint ctrl = (*ip++) & 31u;

            while (true)
            {
                if (ctrl >= 32)
                {
                    uint len = (ctrl >> 5) - 1;
                    uint ofs = (ctrl & 31) << 8;
                    byte* reff = op - ofs - 1;

                    byte code;
                    if (len == 7 - 1)
                    {
                        do
                        {
                            Debug.Assert(ip <= ip_bound);
                            code = *ip++;
                            len += code;
                        }
                        while (code == 255);
                    }

                    code = *ip++;
                    reff -= code;
                    len += 3;

                    /* match from 16-bit distance */
                    if (code == 255)
                    {
                        if ((ofs == (31 << 8)))
                        {
                            Debug.Assert(ip < ip_bound);
                            ofs = (uint)((*ip++) << 8);
                            ofs += *ip++;
                            reff = op - ofs - MAX_L2_DISTANCE - 1;
                        }
                    }

                    Debug.Assert(op + len <= op_limit);
                    Debug.Assert(reff >= (byte*)output);
                    FastLZMove(op, reff, len);
                    op += len;
                }
                else
                {
                    ctrl++;
                    Debug.Assert(op + ctrl <= op_limit);
                    Debug.Assert(ip + ctrl <= ip_limit);
                    Buffer.MemoryCopy(ip, op, ctrl, ctrl);
                    ip += ctrl;
                    op += ctrl;
                }

                if (ip >= ip_limit) break;
                ctrl = *ip++;
            }

            Debug.Assert((int)(op - (byte*)output) == maxout);
        }

        private static unsafe void FastLZMove(byte* dest, byte* src, uint count)
        {
            if ((count > 4) && (dest >= src + count))
            {
                Buffer.MemoryCopy(src, dest, count, count);
            }
            else
            {
                switch (count)
                {
                    default:
                        do { *dest++ = *src++; } while (--count != 0);
                        break;
                    case 3:
                        *dest++ = *src++;
                        break;
                    case 2:
                        *dest++ = *src++;
                        break;
                    case 1:
                        *dest++ = *src++;
                        break;
                    case 0:
                        break;
                }
            }
        }

    }
}
