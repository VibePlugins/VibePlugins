using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness.Execution
{
    /// <summary>
    /// Handles <see cref="RunCodeRequest"/> messages by using reflection to locate
    /// and invoke static methods on the server. Arguments and return values are
    /// serialized as JSON strings.
    /// </summary>
    public static class RemoteInvoker
    {
        /// <summary>
        /// Locates and invokes the static method specified in the request.
        /// The invocation runs on the Unity main thread via <see cref="MainThreadDispatcher"/>.
        /// </summary>
        /// <param name="request">The incoming run-code request.</param>
        /// <returns>A <see cref="RunCodeResponse"/> containing the serialized result or exception info.</returns>
        public static async Task<RunCodeResponse> InvokeAsync(RunCodeRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var response = new RunCodeResponse();

            try
            {
                // Resolve the target type across all loaded assemblies.
                Type targetType = ResolveType(request.TypeName);
                if (targetType == null)
                {
                    response.ExceptionInfo = new SerializableExceptionInfo
                    {
                        Type = typeof(TypeLoadException).FullName,
                        Message = $"Could not resolve type '{request.TypeName}'."
                    };
                    return response;
                }

                // Resolve the method.
                MethodInfo method = targetType.GetMethod(
                    request.MethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (method == null)
                {
                    response.ExceptionInfo = new SerializableExceptionInfo
                    {
                        Type = typeof(MissingMethodException).FullName,
                        Message = $"Could not find static method '{request.MethodName}' on type '{targetType.FullName}'."
                    };
                    return response;
                }

                // Deserialize arguments to match the parameter types.
                ParameterInfo[] parameters = method.GetParameters();
                object[] args = DeserializeArgs(request.SerializedArgs, parameters);

                // Invoke on the main thread.
                object result = await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    return method.Invoke(null, args);
                }).ConfigureAwait(false);

                // If the method returns a Task, await it.
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);

                    // Extract the result from Task<T> if applicable.
                    Type taskType = task.GetType();
                    if (taskType.IsGenericType)
                    {
                        PropertyInfo resultProp = taskType.GetProperty("Result");
                        result = resultProp?.GetValue(task);
                    }
                    else
                    {
                        result = null; // void Task
                    }
                }

                // Serialize the return value.
                if (result != null)
                {
                    response.SerializedResult = JsonConvert.SerializeObject(result);
                }
            }
            catch (TargetInvocationException tie)
            {
                response.ExceptionInfo = SerializableExceptionInfo.FromException(
                    tie.InnerException ?? tie);
            }
            catch (Exception ex)
            {
                response.ExceptionInfo = SerializableExceptionInfo.FromException(ex);
            }

            return response;
        }

        /// <summary>
        /// Resolves a type by its assembly-qualified name or full name, searching
        /// all loaded assemblies if necessary.
        /// </summary>
        private static Type ResolveType(string typeName)
        {
            // Try the standard resolution first (handles assembly-qualified names).
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            // Fall back to searching all loaded assemblies.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        /// <summary>
        /// Deserializes JSON-encoded argument strings into the types expected by the
        /// target method's parameters.
        /// </summary>
        private static object[] DeserializeArgs(string[] serializedArgs, ParameterInfo[] parameters)
        {
            if (serializedArgs == null || serializedArgs.Length == 0)
            {
                return Array.Empty<object>();
            }

            if (serializedArgs.Length != parameters.Length)
            {
                throw new ArgumentException(
                    $"Argument count mismatch: got {serializedArgs.Length}, expected {parameters.Length}.");
            }

            object[] result = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                if (serializedArgs[i] == null)
                {
                    result[i] = null;
                }
                else
                {
                    result[i] = JsonConvert.DeserializeObject(serializedArgs[i], parameters[i].ParameterType);
                }
            }

            return result;
        }
    }
}
