using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.Metrics
{
    public class KureimoMetrics
    {
        public const string MeterName = "Kureimo";

        private readonly Counter<long> _claimsRegistered;
        private readonly Counter<long> _claimsRemoved;
        private readonly Counter<long> _claimConcurrencyRetries;

        public KureimoMetrics()
        {
            var meter = new Meter(MeterName);

            _claimsRegistered = meter.CreateCounter<long>(
                "kureimo.claims.registered", description: "Total de claims registrados com sucesso.");

            _claimsRemoved = meter.CreateCounter<long>(
                "kureimo.claims.removed", description: "Total de claims removidos (unclaim).");

            _claimConcurrencyRetries = meter.CreateCounter<long>(
                "kureimo.claims.concurrency_retries", description: "Retries por conflito de concorrência otimista.");
        }

        public void RecordClaimRegistered() => _claimsRegistered.Add(1);
        public void RecordClaimRemoved() => _claimsRemoved.Add(1);
        public void RecordConcurrencyRetry() => _claimConcurrencyRetries.Add(1);
    }
}
