﻿namespace Polytech.Common.Telemetron
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Polytech.Common.Telemetron;
    using Polytech.Common.Telemetron.Configuration;
    using Polytech.Common.Telemetron.Diagnostics;
    using Polytech.Common.Extension;
    using static Polytech.Common.Telemetron.Diagnostics.DiagnosticTrace;

    /// <summary>
    /// A telemetron to use when testing code. This can be used with TestHarnesses to pull the telemetry that is naturally generated by the application into the app.
    /// </summary>
    public class TraceTelemetron : TextWriterTelemetronBase, ITelemetronProvider<byte[]>
    {

        public TraceTelemetron(ITelemetronConfigurationBase configuration)
            : base(configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
        }

        public IOperation CreateOperation(string operationName)
        {
            try
            {
                ICorrelationContext localCorrelationcontext = this.CorrelationContext;
                long newOperationId = localCorrelationcontext.AddOperation();

                string newOperationIdString = newOperationId.GetBase64String();
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    Diag("Attempting to create operation with null name. Resetting to randomized value. 5NYq0XFr1UM");
                    operationName = "ERR_NO_OPERATION_NAME " + newOperationIdString;
                }

                string cc = localCorrelationcontext.ToString();

                this.CorrelationContext = localCorrelationcontext;

                IOperation createdOperation = new TraceOperation(this, operationName, newOperationIdString, cc);

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

                CorrelationContext localCorrelationcontext = new CorrelationContext(parentContext);

                long newOperationId = localCorrelationcontext.AddOperation();

                string newOperationIdString = newOperationId.GetBase64String();
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    Diag("Attempting to create operation with null name. Resetting to randomized value. 5NYq0XFr1UM");
                    operationName = "ERR_NO_OPERATION_NAME " + newOperationIdString;
                }

                string cc = localCorrelationcontext.ToString();

                this.CorrelationContext = localCorrelationcontext;

                IOperation createdOperation = new TraceOperation(this, operationName, newOperationIdString, cc);

                return createdOperation;
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Instance.Error("An unexpected error occurred when attempting to create an operation", ex, "cd11de1d-c4b6-406c-937e-37bc85eb4370");

                return new NullOperation();
            }
        }

        public override bool Trace(EventSeverity eventSeverity, string message, string codePoint = null, Dictionary<string, string> data = null, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            throw new NotImplementedException();
        }

        public override bool Trace(EventSeverity eventSeverity, string message, Exception exception, string codePoint = null, Dictionary<string, string> data = null, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            DateTime now = DateTime.UtcNow;

            try
            {
                string log = this.CreateLogMessage(eventSeverity, message, codePoint, data, callerMemberName, callerFilePath, callerLineNumber, now);

                switch (eventSeverity)
                {
                    case EventSeverity.Debug:
                    case EventSeverity.Verbose:
                    case EventSeverity.Info:
                    case EventSeverity.Event:
                    case EventSeverity.Metric:
                    case EventSeverity.OperationInfo:
                    default:
                        global::System.Diagnostics.Trace.TraceInformation(log);
                        break;
                    case EventSeverity.Warning:
                        global::System.Diagnostics.Trace.TraceWarning(log);
                        break;
                    case EventSeverity.Error:
                    case EventSeverity.OperationError:
                    case EventSeverity.Fatal:
                        global::System.Diagnostics.Trace.TraceError(log);
                        break;

                }

                return true;
            }
            catch (Exception ex)
            when (Filter("An unexpected exception occurred when attempting to emit telemetry to the test context.", ex))
            {
                return false;
            }
        }


        /// <summary>
        /// Repply the origin context captured as part of an operation.
        /// </summary>
        /// <param name="capturedContext">the context to reapply.</param>
        void ICorrelatedProvider.ReapplyOriginContext(byte[] capturedContext)
        {
            this.CorrelationContext = new CorrelationContext(capturedContext);
        }
    }
}
