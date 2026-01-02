using System.Runtime.CompilerServices;

namespace VECS
{
    [InlineArray(Presenter.MAX_CAMERAS)]
    public struct BufferMAXCAMS<T> where T : unmanaged
    {
        private T element0;
    }
}
