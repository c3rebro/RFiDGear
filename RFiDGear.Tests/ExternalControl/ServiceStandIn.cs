#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

using RFiDGear.Bridge;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// A stand-in for the service of a bridge client. It forwards every call to the real service and counts it, and a test can
    /// replace one method: to make a request slow, or to have RFiDGear say something it does not say.
    /// </summary>
    public class ServiceStandIn : DispatchProxy
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, Func<IBridgeService, object?[], object?>> _replacements = new Dictionary<string, Func<IBridgeService, object?[], object?>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _calls = new Dictionary<string, int>(StringComparer.Ordinal);
        private IBridgeService? _target;

        /// <summary>Puts a stand-in in front of a service.</summary>
        /// <param name="target">The real service.</param>
        /// <param name="service">The service to give to the code under test.</param>
        /// <returns>The stand-in, to replace methods and to count calls.</returns>
        public static ServiceStandIn Wrap(IBridgeService target, out IBridgeService service)
        {
            service = Create<IBridgeService, ServiceStandIn>();
            var stand = (ServiceStandIn)(object)service;
            stand._target = target;

            return stand;
        }

        /// <summary>Replaces what a method does; the replacement gets the real service, so that it can forward a call.</summary>
        /// <param name="method">The name of the method.</param>
        /// <param name="replacement">What it does instead: the real service and the arguments, and the result the method returns.</param>
        public void Replace(string method, Func<IBridgeService, object?[], object?> replacement)
        {
            lock (_gate)
            {
                _replacements[method] = replacement;
            }
        }

        /// <summary>Counts the calls of a method, the replaced ones included.</summary>
        /// <param name="method">The name of the method.</param>
        /// <returns>How often it was called.</returns>
        public int CallsOf(string method)
        {
            lock (_gate)
            {
                return _calls.GetValueOrDefault(method);
            }
        }

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            var arguments = args ?? Array.Empty<object?>();
            Func<IBridgeService, object?[], object?>? replacement;

            lock (_gate)
            {
                _calls[targetMethod.Name] = _calls.GetValueOrDefault(targetMethod.Name) + 1;
                _replacements.TryGetValue(targetMethod.Name, out replacement);
            }

            if (replacement != null)
            {
                return replacement(_target!, arguments);
            }

            try
            {
                return targetMethod.Invoke(_target, arguments);
            }
            catch (TargetInvocationException failure) when (failure.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(failure.InnerException).Throw();

                throw;
            }
        }
    }
}
