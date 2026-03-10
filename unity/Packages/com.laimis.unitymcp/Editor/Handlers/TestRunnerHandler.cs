#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using static UnityMcp.Editor.UnityMcpParameterHelpers;

namespace UnityMcp.Editor
{
    internal static class TestRunnerHandler
    {
        // State tracking for test runs
        private static bool _testRunInProgress;
        private static string? _lastTestMode;
        private static object? _lastTestResults;
        private static TestRunnerApi? _activeTestRunnerApi;

        internal static string BuildListTestsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "testRunner.listTests");
            var mode = ParseRequiredStringParameter(paramsObject, "mode");
            var testMode = ParseTestMode(mode);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            ITestAdaptor? testTree = null;

            api.RetrieveTestList(testMode, adaptor => { testTree = adaptor; });

            if (testTree == null)
            {
                var result = new
                {
                    mode,
                    count = 0,
                    tests = new List<object>()
                };

                return UnityMcpProtocol.CreateResult(idToken, result);
            }

            var tests = new List<object>();
            CollectTestCases(testTree, tests);

            var response = new
            {
                mode,
                count = tests.Count,
                tests
            };

            return UnityMcpProtocol.CreateResult(idToken, response);
        }

        internal static string BuildRunTestsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "testRunner.run");
            var mode = ParseRequiredStringParameter(paramsObject, "mode");
            var testFilter = ParseOptionalStringParameter(paramsObject, "testFilter");
            var testMode = ParseTestMode(mode);

            if (_testRunInProgress)
            {
                throw new ArgumentException("A test run is already in progress. Use testRunner.cancel to stop it, or testRunner.getResults to check status.");
            }

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _activeTestRunnerApi = api;
            _testRunInProgress = true;
            _lastTestMode = mode;
            _lastTestResults = null;

            var callbacks = new TestRunCallbacks();
            api.RegisterCallbacks(callbacks);

            var filter = new Filter
            {
                testMode = testMode
            };

            if (!string.IsNullOrEmpty(testFilter))
            {
                filter.testNames = new[] { testFilter };
            }

            api.Execute(new ExecutionSettings(filter));

            var result = new
            {
                started = true,
                mode,
                testFilter = testFilter ?? (object?)null,
                message = $"Test run started in {mode} mode. Use testRunner.getResults to poll for results."
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildGetTestResultsResponse(JToken idToken)
        {
            if (_testRunInProgress)
            {
                var result = new
                {
                    status = "running",
                    mode = _lastTestMode,
                    message = "Test run is still in progress."
                };

                return UnityMcpProtocol.CreateResult(idToken, result);
            }

            if (_lastTestResults == null)
            {
                var result = new
                {
                    status = "none",
                    message = "No test results available. Use testRunner.run to start a test run."
                };

                return UnityMcpProtocol.CreateResult(idToken, result);
            }

            return UnityMcpProtocol.CreateResult(idToken, _lastTestResults);
        }

        internal static string BuildCancelTestRunResponse(JToken idToken)
        {
            if (!_testRunInProgress)
            {
                var result = new
                {
                    cancelled = false,
                    message = "No test run is currently in progress."
                };

                return UnityMcpProtocol.CreateResult(idToken, result);
            }

            // Unity Test Runner API does not expose a direct cancel method.
            // Reset state so a new run can be started.
            _testRunInProgress = false;

            var response = new
            {
                cancelled = true,
                message = "Test run state has been reset. Note: the underlying Unity test execution may continue to completion."
            };

            return UnityMcpProtocol.CreateResult(idToken, response);
        }

        // Helper methods

        private static TestMode ParseTestMode(string mode)
        {
            return mode.ToLowerInvariant() switch
            {
                "editmode" => TestMode.EditMode,
                "playmode" => TestMode.PlayMode,
                _ => throw new ArgumentException($"Invalid test mode '{mode}'. Valid modes are 'EditMode' and 'PlayMode'.")
            };
        }

        private static void CollectTestCases(ITestAdaptor adaptor, List<object> tests)
        {
            if (!adaptor.HasChildren)
            {
                tests.Add(new
                {
                    fullName = adaptor.FullName,
                    name = adaptor.Name,
                    typeName = adaptor.TypeInfo?.FullName,
                    methodName = adaptor.Name,
                    testCaseCount = adaptor.TestCaseCount,
                    runState = adaptor.RunState.ToString()
                });

                return;
            }

            foreach (var child in adaptor.Children)
            {
                CollectTestCases(child, tests);
            }
        }

        private static object BuildTestResultData(ITestResultAdaptor result)
        {
            var testResults = new List<object>();
            var counts = new TestResultCounts();
            CollectTestResults(result, testResults, counts);

            return new
            {
                status = "completed",
                mode = _lastTestMode,
                summary = new
                {
                    total = testResults.Count,
                    passed = counts.Passed,
                    failed = counts.Failed,
                    skipped = counts.Skipped,
                    inconclusive = counts.Inconclusive
                },
                tests = testResults
            };
        }

        private static void CollectTestResults(ITestResultAdaptor result, List<object> results, TestResultCounts counts)
        {
            if (!result.HasChildren)
            {
                var state = result.ResultState.ToString();

                if (state.Contains("Pass", StringComparison.OrdinalIgnoreCase))
                {
                    counts.Passed++;
                }
                else if (state.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
                         state.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    counts.Failed++;
                }
                else if (state.Contains("Skip", StringComparison.OrdinalIgnoreCase) ||
                         state.Contains("Ignore", StringComparison.OrdinalIgnoreCase))
                {
                    counts.Skipped++;
                }
                else if (state.Contains("Inconclusive", StringComparison.OrdinalIgnoreCase))
                {
                    counts.Inconclusive++;
                }

                results.Add(new
                {
                    fullName = result.FullName,
                    name = result.Name,
                    resultState = state,
                    duration = result.Duration,
                    startTime = result.StartTime?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    endTime = result.EndTime?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    message = result.Message,
                    stackTrace = result.StackTrace
                });

                return;
            }

            foreach (var child in result.Children)
            {
                CollectTestResults(child, results, counts);
            }
        }

        private sealed class TestRunCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                // Test run has started
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _lastTestResults = BuildTestResultData(result);
                _testRunInProgress = false;

                if (_activeTestRunnerApi != null)
                {
                    _activeTestRunnerApi.UnregisterCallbacks(this);
                }
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }

        private sealed class TestResultCounts
        {
            public int Passed;
            public int Failed;
            public int Skipped;
            public int Inconclusive;
        }
    }
}