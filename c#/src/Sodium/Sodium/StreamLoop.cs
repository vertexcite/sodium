using System;

namespace Sodium
{
    /// <summary>
    ///     A forward reference for a <see cref="Stream{T}" /> equivalent to the <see cref="Stream{T}" /> that is referenced.
    /// </summary>
    /// <typeparam name="T">The type of values fired by the stream.</typeparam>
    public class StreamLoop<T> : Stream<T>
    {
        private readonly object isAssignedLock = new object();
        private bool isAssigned;

        /// <summary>
        ///     Create an <see cref="StreamLoop{T}" />.  This must be called from within a transaction.
        /// </summary>
        public StreamLoop()
        {
            if (!Transaction.HasCurrentTransaction())
            {
                throw new InvalidOperationException("StreamLoop and CellLoop must be used within an explicit transaction");
            }
        }

        internal bool IsAssigned
        {
            get
            {
                lock (this.isAssignedLock)
                {
                    return this.isAssigned;
                }
            }
        }

        /// <summary>
        ///     Resolve the loop to specify what the <see cref="StreamLoop{T}" /> was a forward reference to.  This method
        ///     must be called inside the same transaction as the one in which this <see cref="StreamLoop{T}" /> instance was
        ///     created and used.
        ///     This requires an explicit transaction to be created with <see cref="Transaction.Run{T}(Func{T})" /> or
        ///     <see cref="Transaction.RunVoid(Action)" />.
        /// </summary>
        /// <param name="stream">The stream that was forward referenced.</param>
        public void Loop(Stream<T> stream)
        {
            lock (this.isAssignedLock)
            {
                if (this.isAssigned)
                {
                    throw new InvalidOperationException("StreamLoop was looped more than once.");
                }

                this.isAssigned = true;
            }

            Transaction.RunVoid(() => this.UnsafeAddCleanup(stream.Listen(this.Node, this.Send)));
        }
    }
}