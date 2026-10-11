import java.lang.foreign.Arena;
import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.SymbolLookup;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;

// Diagnostic Java 25 only: a DIRECT child is created with bInheritHandles=FALSE.
// No breakaway flag: Windows atomically inherits the parent's Job membership.
public final class HandlelessChild {
    public static void main(String[] args) throws Throwable {
        Files.write(Paths.get(args[1] + ".tmp"), Long.toString(ProcessHandle.current().pid()).getBytes(StandardCharsets.UTF_8));
        Files.move(Paths.get(args[1] + ".tmp"), Paths.get(args[1]), StandardCopyOption.ATOMIC_MOVE);
        try (Arena arena = Arena.ofConfined()) {
            SymbolLookup kernel = SymbolLookup.libraryLookup("kernel32", arena);
            Linker linker = Linker.nativeLinker();
            MethodHandle create = linker.downcallHandle(kernel.find("CreateProcessW").orElseThrow(),
                FunctionDescriptor.of(ValueLayout.JAVA_INT,
                    ValueLayout.ADDRESS, ValueLayout.ADDRESS, ValueLayout.ADDRESS, ValueLayout.ADDRESS,
                    ValueLayout.JAVA_INT, ValueLayout.JAVA_INT, ValueLayout.ADDRESS, ValueLayout.ADDRESS,
                    ValueLayout.ADDRESS, ValueLayout.ADDRESS));
            MethodHandle close = linker.downcallHandle(kernel.find("CloseHandle").orElseThrow(),
                FunctionDescriptor.of(ValueLayout.JAVA_INT, ValueLayout.ADDRESS));
            MethodHandle error = linker.downcallHandle(kernel.find("GetLastError").orElseThrow(),
                FunctionDescriptor.of(ValueLayout.JAVA_INT));
            String java = System.getProperty("java.home") + "\\bin\\java.exe";
            String command = quote(java) + " -cp " + quote(System.getProperty("java.class.path"))
                + " JobWitness child " + quote(args[1] + ".child") + " " + quote(args[2] + ".child");
            MemorySegment startup = arena.allocate(104, 8); // Windows x64 STARTUPINFOW
            startup.set(ValueLayout.JAVA_INT, 0, 104);
            MemorySegment information = arena.allocate(24, 8);
            int success = (int) create.invokeExact(wide(arena, java), wide(arena, command),
                MemorySegment.NULL, MemorySegment.NULL, 0, 0x08000400,
                MemorySegment.NULL, MemorySegment.NULL, startup, information);
            if (success == 0) {
                throw new IllegalStateException("CreateProcessW failed: " + (int) error.invokeExact());
            }
            int closeThread = (int) close.invokeExact(information.get(ValueLayout.ADDRESS, 8));
            int closeProcess = (int) close.invokeExact(information.get(ValueLayout.ADDRESS, 0));
            if (closeThread == 0 || closeProcess == 0) {
                throw new IllegalStateException("CloseHandle failed");
            }
        }
        while (!Files.exists(Paths.get(args[2]))) {
            Thread.sleep(20);
        }
        System.exit(37); // Child is intentionally NOT awaited.
    }

    private static MemorySegment wide(Arena arena, String value) {
        byte[] bytes = (value + "\0").getBytes(StandardCharsets.UTF_16LE);
        MemorySegment buffer = arena.allocate(bytes.length, 2);
        buffer.copyFrom(MemorySegment.ofArray(bytes));
        return buffer;
    }

    private static String quote(String value) {
        // Test paths contain no quotes and do not end with a separator.
        return "\"" + value + "\"";
    }
}
