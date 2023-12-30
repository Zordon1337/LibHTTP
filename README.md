# Basic HTTP Library written in C#(.NET FRAMEWORK)

### Example code screenshot
![image](https://github.com/Zordon1337/LibHTTP/assets/65111609/2662a3c5-b319-4c56-974e-8e3b6fe6274d)


### Example usage(Multiple addresses)
```csharp
// Initalizing HTTP Server
HTTP http = new HTTP();
// Creating Thread so it will continue executing rest of the script(including http.GET)
Thread ServerThread = new Thread(() => http.ListenMA(new string[] { "http://127.0.0.1:80/", "http://localhost:80/" }));
// starting the thread
ServerThread.Start();
// adding paths and handlers to routes list
http.Get("/", "text/plain", queryparams => "pong");
// adding this to not close the whole server after initalizing
Console.ReadLine(); 
```
### Example usage(Single address)
```csharp
// Initalizing HTTP Server
HTTP http = new HTTP();
// Creating Thread so it will continue executing rest of the script(including http.GET)
Thread ServerThread = new Thread(() => http.Listen("http://localhost:80/"));
// starting the thread
ServerThread.Start();
// adding paths and handlers to routes list
http.Get("/", "text/plain", queryparams => "pong");
// adding this to not close the whole server after initalizing
Console.ReadLine(); 
```

### More examples will be in wiki

## Requirements
``
.NET Framework 4.7.2 or newer
``
