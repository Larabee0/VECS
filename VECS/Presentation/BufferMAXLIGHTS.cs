using System.Runtime.CompilerServices;

namespace VECS
{
    [InlineArray(Presenter.MAX_LIGHTS)]
    public struct BufferMAXLIGHTS<T> where T : unmanaged
    {
        private T element0;
    }
}
