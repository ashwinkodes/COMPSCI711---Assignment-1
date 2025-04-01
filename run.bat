csc /out:network\network.exe network\network.cs 

csc /out:Middleware1\middleware8082.exe Middleware1\middleware8082.cs
csc /out:Middleware2\middleware8083.exe Middleware2\middleware8083.cs
csc /out:Middleware3\middleware8084.exe Middleware3\middleware8084.cs
csc /out:Middleware4\middleware8085.exe Middleware4\middleware8085.cs
csc /out:Middleware5\middleware8086.exe Middleware5\middleware8086.cs

start .\network\network.exe

start Middleware1\middleware8082.exe
start Middleware2\middleware8083.exe
start Middleware3\middleware8084.exe
start Middleware4\middleware8085.exe
start Middleware5\middleware8086.exe