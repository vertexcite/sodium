using System;
using System.Collections.Generic;

namespace Sodium.Time
{
    public class TimerSystem<T> : ITimerSystem<T> where T : IComparable<T>
    {
        public TimerSystem(ITimerSystemImplementation<T> implementation, Action<Exception> handleException)
        {
            this.implementation = implementation;
            this.implementation.Start(handleException);
            CellSink<T> timeSink = new CellSink<T>(this.implementation.Now);
            this.Time = timeSink;
            Transaction.OnStart(() =>
            {
                T t = this.implementation.Now;
                this.implementation.RunTimersTo(t);
                while (true)
                {
                    Event ev = null;
                    // Pop all events earlier than t.
                    lock (this.eventQueue)
                    {
                        if (this.eventQueue.Count > 0)
                        {
                            Event tempEvent = this.eventQueue.Peek();
                            if (tempEvent != null && tempEvent.Time.CompareTo(t) <= 0)
                            {
                                ev = this.eventQueue.Dequeue();
                            }
                        }
                    }

                    if (ev != null)
                    {
                        timeSink.Send(ev.Time);
                        ev.Alarm.Send(ev.Time);
                    }
                    else
                        break;
                }

                timeSink.Send(t);
            });
        }

        private readonly ITimerSystemImplementation<T> implementation;

        /// <summary>
        ///     Gets a cell giving the current clock time.
        /// </summary>
        public Cell<T> Time { get; }

        private class Event
        {
            internal Event(T time, StreamSink<T> alarm)
            {
                this.Time = time;
                this.Alarm = alarm;
            }

            internal readonly T Time;
            internal readonly StreamSink<T> Alarm;
        };

        private readonly Queue<Event> eventQueue = new Queue<Event>();

        private class CurrentTimer
        {
            internal IMaybe<ITimer> Timer = Maybe.Nothing<ITimer>();
        };

        /// <summary>
        ///     A timer that fires at the specified time.
        /// </summary>
        /// <param name="t">The time to fire at.</param>
        /// <returns>A stream which fires at the specified time.</returns>
        public Stream<T> At(Cell<IMaybe<T>> t)
        {
            StreamSink<T> alarm = new StreamSink<T>();
            CurrentTimer current = new CurrentTimer();
            IListener l = t.Listen(m =>
            {
                current.Timer.Match(timer => timer.Cancel(), () => { });
                current.Timer = m.Match(
                    timer => Maybe.Just(this.implementation.SetTimer(timer, () =>
                    {
                        lock (this.eventQueue)
                        {
                            this.eventQueue.Enqueue(new Event(timer, alarm));
                        }
                        // Open and close a transaction to trigger queued
                        // events to run.
                        Transaction.RunVoid(() => { });
                    })),
                    Maybe.Nothing<ITimer>);
            });
            return alarm.AddCleanup(l);
        }
    }
}