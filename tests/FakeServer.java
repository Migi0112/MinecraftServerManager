import java.io.*;
import java.nio.charset.StandardCharsets;

// A protocol fixture, not a Minecraft server. Used only by the verification project.
public class FakeServer {
    public static void main(String[] args) throws Exception {
        System.out.println("[Server thread/INFO]: Done (1.234s)! For help, type \"help\"");
        BufferedReader input = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
        String line;
        while ((line = input.readLine()) != null) {
            switch (line) {
                case "stop": System.out.println("Saving worlds"); return;
                case "crash": System.err.println("Simulated crash"); System.exit(7); break;
                case "list": System.out.println("There are 2 of a max of 20 players online: Alex, Steve"); break;
                case "neoforge tps": System.out.println("Overall: 19.875 TPS (12.345 ms/tick)"); break;
                default: System.out.println("ECHO: " + line);
            }
        }
    }
}
