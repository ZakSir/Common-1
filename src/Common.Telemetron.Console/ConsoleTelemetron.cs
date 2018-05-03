﻿namespace Polytech.Telemetron
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Polytech.Common.Telemetron;
    using Polytech.Common.Telemetron.Configuration;
    using Polytech.Common.Telemetron.Diagnostics;

    public partial class ConsoleTelemetron : CorrelatedProviderBase, ITelemetronProvider<byte[]>, IDisposable
    {
        private ConcurrentQueue<ConsoleEvent> eventQueue;
        private Task queueTask;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        
        /// <summary>
        /// Fires each time that an event is processed. Used for testing.
        /// </summary>
        internal event EventHandler<ConsoleEvent> EventEnqueued;

        public ConsoleTelemetron(IConsoleConfiguration configuration)
            : base(configuration)
        {
            this.CopyConfigLocal(configuration);
            
            this.CorrelationContext = new CorrelationContext(1337);

            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.eventQueue = new ConcurrentQueue<ConsoleEvent>();

            this.queueTask = this.QueueJob(this.cancellationToken);
        }

        public IOperation CreateOperation(string operationName)
        {
            try
            {
                ICorrelationContext localCorrelationcontext = this.CorrelationContext;
                long newOperationId = localCorrelationcontext.AddOperation();
                string cc = localCorrelationcontext.ToString();

                this.CorrelationContext = localCorrelationcontext;

                IOperation createdOperation = new ConsoleOperation(this, operationName, newOperationId.GetBase64String(), cc);

                return createdOperation;
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Instance.Error("An unexpected error occurred when attempting to create an operation", ex, "1f3803c8-5a8c-4562-a96b-7069520d8e32");

                return new NullOperation();
            }

        }

        public IOperation CreateOperation(string operationName, byte[] parentContext)
        {
            try
            {
                byte[] capturedCorrelationContext = this.CorrelationContext.Capture();

                try
                {
                    CorrelationContext localCorrelationcontext = new CorrelationContext(parentContext);

                    long newOperationId = localCorrelationcontext.AddOperation();
                    string cc = localCorrelationcontext.ToString();

                    this.CorrelationContext = localCorrelationcontext;

                    IOperation createdOperation = new ConsoleOperation(this, operationName, newOperationId.GetBase64String(), cc);

                    return createdOperation;
                }
                catch (Exception ex)
                {
                    DiagnosticTrace.Instance.Error("An unexpected error occurred when attempting to create an operation", ex, "cd11de1d-c4b6-406c-937e-37bc85eb4370");

                    return new NullOperation();
                }
                finally
                {
                    this.CorrelationContext = new CorrelationContext(capturedCorrelationContext);
                }
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Instance.Error("An unexpected error occurred when attempting to reinstate the correlation context", ex, "cb64280a-daa2-43e5-b4f5-fc69f7dbfeb1");

                return new NullOperation();
            }
        }

        public bool Event(string name, Dictionary<string, string> data = null)
        {
            try
            {
                this.Trace(EventSeverity.Event, name, "event", data, string.Empty, string.Empty, -1);
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Instance.Error("An unexpected error occurred when attempting emit an event", ex, "0df8b1e6-38b5-4790-ad92-6f5e7da9ce3a");
                return false;
            }
        }


        public bool Metric(string name, double value = 1, Dictionary<string, string> data = null)
        {
            try
            {
                this.Trace(EventSeverity.Metric, $"{name}:{value}", "metric", data, string.Empty, string.Empty, -1);
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Instance.Error("An unexpected error occurred when attempting emit a metric", ex, "70280d83-876d-4cd8-86ff-95e639e63b94");
                return false;
            }
        }


        public bool Trace(EventSeverity eventSeverity, string message, Exception exception, string codePoint = null, Dictionary<string, string> data = null, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            // safe bridge
            return this.Trace(eventSeverity, message, codePoint, data.SafeCombine(exception), callerMemberName, callerFilePath, callerLineNumber);
        }

        public bool Trace(EventSeverity eventSeverity, string message, string codePoint = null, Dictionary<string, string> data = null, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            try
            {
                DateTime eventTime = DateTime.UtcNow;

                ConsoleEvent evt = new ConsoleEvent()
                {
                    CallerFilePath = callerFilePath,
                    CallerLineNumber = callerLineNumber,
                    CallerMemberName = callerMemberName,
                    CodePoint = codePoint,
                    Data = data,
                    EventSeverity = eventSeverity,
                    EventTime = eventTime,
                    Message = message,
                    CorrelationContext = this.CorrelationContext.ToString()
                };

                this.eventQueue.Enqueue(evt);
                this.OnEventQueued(evt);

                return true;
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Instance.Error("An unexpected exception has occured when attemtping to enqueue a log event, the event has been lost. ", ex, "DSA7WAaQrUM");
                return false;
            }
        }

        internal virtual void OnEventQueued(ConsoleEvent ce)
        {
            if (this.EventEnqueued != null)
            {
                this.EventEnqueued(this, ce);
            }
        }

        private async Task QueueJob(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                if (this.eventQueue.IsEmpty)
                {
                    await Task.Delay(100);
                }
                else
                {
                    if (this.eventQueue.TryDequeue(out ConsoleEvent evt))
                    {
                        this.TraceToConsole(evt.EventSeverity, evt.Message, evt.CodePoint, evt.Data, evt.CallerMemberName, evt.CallerFilePath, evt.CallerLineNumber, evt.EventTime, evt.CorrelationContext);
                    }
                }
            }
        }

        private void TraceToConsole(EventSeverity eventSeverity, string message, string codePoint, Dictionary<string, string> data, string callerMemberName, string callerFilePath, int callerLineNumber, DateTime eventTime, string correlationContext)
        {
            try
            {
                if (data == null || !this.EmitAdditionalData)
                {
                    data = new Dictionary<string, string>();
                }

                if (this.EmitCodePoint)
                {
                    if (codePoint == null)
                    {
                        if (this.NullCodepointAction == EmptyCodepointAction.DoNothing)
                        {
                            // dont add to dictionary as it does not exist.
                        }
                        else
                        {
                            data[nameof(codePoint)] = this.NullCodepointActionDelegate(message, data, callerMemberName, callerFilePath, callerLineNumber);
                        }
                    }
                    else
                    {
                        data[nameof(codePoint)] = codePoint;
                    }
                }

                if (this.EmitCallerMemberName)
                {
                    data[nameof(callerMemberName)] = callerMemberName;
                }

                if (this.EmitCallerFilePath)
                {
                    data[nameof(callerFilePath)] = callerFilePath;
                }

                if (this.EmitCallerLineNumber)
                {
                    data[nameof(callerLineNumber)] = callerLineNumber.ToString();
                }

                if (this.EmitCorrelationContext)
                {
                    data[nameof(CorrelationContext)] = correlationContext;
                }

                // write event
                ConsoleColor foregroundColor = this.GetForegroundColor(eventSeverity);
                ConsoleColor backgroundColor = this.GetBackgroundColor(eventSeverity);

                Console.ResetColor();
                Console.Write('[');
                Write(GetTimeString(eventTime), foregroundColor, backgroundColor);
                Console.Write("][");
                Write(eventSeverity.ToString(), foregroundColor, backgroundColor);
                Console.Write(']');
                WriteLine(message, foregroundColor, backgroundColor);

                if (data.Count > 0)
                {
                    Write("Additional Data: ", foregroundColor, backgroundColor);
                    WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented), ConsoleColor.DarkGray, ConsoleColor.Black);
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Instance.Error("An unexpected exception has occurred when attempting to send data to the console. See the Details for more information.", ex, "sOe8HdhzzkM");
            }
        }

        private static void Write(string message, ConsoleColor foreground, ConsoleColor background)
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
            Console.Write(message);
            Console.ResetColor();
        }

        private static void WriteLine(string message, ConsoleColor foreground, ConsoleColor background)
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static string GetTimeString(DateTime dt)
        {
            if (dt.Hour == 0 && dt.Minute == 0)
            {
                // midnight
                return dt.ToString("MMM-dd-YY HH:mm:ss.fff") + " midnight";
            }
            else
            {
                return dt.ToString("HH:mm:ss.fff");
            }
        }

        public void Dispose()
        {
            this.cancellationTokenSource.CancelAfter(300);

            Thread.Sleep(300);
        }
    }
}
