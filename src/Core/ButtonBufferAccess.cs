using System;
using System.Reflection;
using System.Reflection.Emit;

namespace CommandQueueRemastered.Core
{
    public static class ButtonBufferAccess
    {
        // Build a typed setter once, since the game's nested ButtonBuffer type is private.
        // Clear only suppression latches; raw values and previous polling states stay intact.
        public static Action<T> CreateReleaseAction<T>() where T : class
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var buffers = typeof(T).GetField("buttonBuffers", fields);
            if (buffers == null || !buffers.FieldType.IsValueType)
                throw new InvalidOperationException("Unsupported gameplay buttonBuffers layout.");
            var buttons = buffers.FieldType.GetFields(fields);
            if (buttons.Length == 0) throw new InvalidOperationException("No gameplay button buffers found.");
            var method = new DynamicMethod("ReleaseGameplayButtonSuppression", typeof(void), new[] { typeof(T) }, typeof(T), true);
            var il = method.GetILGenerator();
            foreach (var button in buttons)
            {
                var suppressed = button.FieldType.GetField("suppressUntilReleased", fields);
                if (!button.FieldType.IsValueType || suppressed == null || suppressed.FieldType != typeof(bool))
                    throw new InvalidOperationException("Unsupported gameplay buffer: " + button.Name);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, buffers);
                il.Emit(OpCodes.Ldflda, button);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stfld, suppressed);
            }
            il.Emit(OpCodes.Ret);
            return (Action<T>)method.CreateDelegate(typeof(Action<T>));
        }
    }
}
