import java.io.File;
import java.lang.management.ManagementFactory;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.util.Arrays;

// Standalone diagnostic JVM, never part of Minecraft or the Launcher runtime.
public final class JobWitness {
    public static void main(String[] args) throws Exception {
        String pid = ManagementFactory.getRuntimeMXBean().getName().split("@")[0];
        System.out.println("PID=" + pid);
        System.out.println("ARGS=" + Arrays.toString(args));
        System.out.println("ENV=" + System.getenv("JOB_PROTOTYPE_VALUE"));
        System.out.println("CWD=" + new File(".").getCanonicalPath());
        System.err.println("STDERR=job-witness");
        System.out.flush();
        System.err.flush();
        Files.write(Paths.get(args[1] + ".tmp"), pid.getBytes(StandardCharsets.UTF_8));
        Files.move(Paths.get(args[1] + ".tmp"), Paths.get(args[1]), StandardCopyOption.ATOMIC_MOVE);
        if (args[0].equals("spawn")) {
            // Descendants remain inside the guarded Job and may inherit QUERY witness.
            new ProcessBuilder(
                System.getProperty("java.home") + File.separator + "bin" + File.separator + "java.exe",
                "-cp", System.getProperty("java.class.path"), "JobWitness", "child",
                args[1] + ".child", args[2] + ".child").start();
            while (!Files.exists(Paths.get(args[2]))) {
                Thread.sleep(20);
            }
        } else {
            while (!Files.exists(Paths.get(args[2]))) {
                Thread.sleep(20);
            }
        }
        System.exit(37);
    }
}
