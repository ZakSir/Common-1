using Microsoft.VisualStudio.TestTools.UnitTesting;
using Polytech.Common.Telemetron.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Polytech.Common.Telemetron
{
    public class TraceListenerHarness : IDisposable, IList<TraceEventEvent>
    {
        private static readonly char[] spaceSplitChars = new char[] {' '};
        private TraceSource source;
        private HarnessTraceListener listener;


        public TraceListenerHarness(TraceSource source)
        {
            this.listener = new HarnessTraceListener();

            this.source = source;
            this.source.Listeners.Add(this.listener);
        }

        public IList<TraceEventEvent> Events => this.listener.Events;

        public int Count => this.Events.Count;

        public bool IsReadOnly => this.Events.IsReadOnly;

        public TraceEventEvent this[int index] { get => this.Events[index]; set => this.Events[index] = value; }

        public void Dispose()
        {
            this.source.Listeners.Remove(this.listener);
            this.listener.Dispose();
        }

        public void AssertContains(string partial)
        {
            if (!this.Contains(partial, out string actual))
            {
                Assert.Fail($"The string '{partial}' was not found (case insensitive) in any of the events emitted through the trace. This string was expected.");
            }
        }

        public void AssertNotContains(string partial)
        {
            if (this.Contains(partial, out string actual))
            {
                Assert.Fail($"The string '{partial}' was found (case insensitive) and was expected to be missing from the trace. Actual <{actual}>.");
            }
        }

        public void AssertCount(int count)
        {
            Assert.AreEqual(count, this.listener.Events.Count);
        }

        public IEnumerable<string> GetData(string eventType)
        {
            return this.Events
                .ToArray() // Copy 
                .Where(e => e.Actual.Contains(eventType) && e.Data != null)
                .Select(e => e.Data);
        }

        public void AssertDataAreEqual(string eventType, int position, long expected)
        {
            IEnumerable<string> datas = this.GetData(eventType);

            foreach (string data in datas)
            {
                string[] parts = data.Split(spaceSplitChars, StringSplitOptions.RemoveEmptyEntries);

                if (long.TryParse(parts[position], out long foundValue))
                {
                    if (foundValue == expected)
                    {
                        return;
                    }
                }
            }

            if (datas.Any())
            {
                Assert.Fail($"Found {datas.Count()} rows, but none of them parsed into the data expected <{expected}>");
            }
            else
            {
                Assert.Fail($"Could not find any rows with the expected type <{eventType}>");
            }
        }

        private bool Contains(string partial, out string firstActual)
        {
            string partialUpper = partial.ToUpperInvariant();

            string payload = this.listener.Events.FirstOrDefault(e => e.Upper.Contains(partialUpper)).Actual;

            if (payload != null)
            {
                firstActual = payload;
                return true;
            }
            else
            {
                firstActual = null;
                return false;
            }
        }

        public int IndexOf(TraceEventEvent item)
        {
            return this.Events.IndexOf(item);
        }

        public void Insert(int index, TraceEventEvent item)
        {
            this.Events.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            this.Events.RemoveAt(index);
        }

        public void Add(TraceEventEvent item)
        {
            this.Events.Add(item);
        }

        public void Clear()
        {
            this.Events.Clear();
        }

        public bool Contains(TraceEventEvent item)
        {
            return this.Events.Contains(item);
        }

        public void CopyTo(TraceEventEvent[] array, int arrayIndex)
        {
            this.Events.CopyTo(array, arrayIndex);
        }

        public bool Remove(TraceEventEvent item)
        {
            return this.Events.Remove(item);
        }

        public IEnumerator<TraceEventEvent> GetEnumerator()
        {
            return this.Events.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.Events.GetEnumerator();
        }

        internal class HarnessTraceListener : TraceListener
        {
            private StringBuilder builder;
            private volatile bool isBuilding;
            private List<TraceEventEvent> events = new List<TraceEventEvent>();

            internal List<TraceEventEvent> Events => this.events;

            public override void Write(string message)
            {
                if (this.isBuilding == false)
                {
                    this.isBuilding = true;
                    this.builder = new StringBuilder();
                }

                this.builder.Append(message);
            }

            public override void WriteLine(string message)
            {
                if (this.isBuilding)
                {
                    this.builder.Append(message);
                    this.events.Add(new TraceEventEvent(this.builder.ToString()));
                    this.isBuilding = false;
                }
                else
                {
                    this.events.Add(new TraceEventEvent(message));
                }
            }

            protected override void Dispose(bool disposing)
            {
                this.events.Clear();

                base.Dispose(disposing);
            }
        }


    }
}
