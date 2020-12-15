open System;  
open System.Net;  
open System.Net.Sockets;  
open System.Threading;  
open System.Text; 

open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open System
open System.Text

let socket = new ClientWebSocket()
let cts = new CancellationTokenSource()
let uri = Uri("ws://localhost:8080/websocket")

let aa = socket.ConnectAsync(uri, cts.Token)

aa.Wait()

// let a = ArraySegment<byte>[|byte('1'); byte('2'); byte('3')|]
let a = Encoding.ASCII.GetBytes("123") |> ArraySegment<byte>
let aaa = socket.SendAsync (a, WebSocketMessageType.Text, true, cts.Token)
aaa.Wait()
// let mutable buffer: ArraySegment<byte> = ArraySegment.Empty
let buffer = WebSocket.CreateClientBuffer(20, 20)
let mutable xx = false
let mutable dd = null
// while not(xx) do
Console.WriteLine("HERE 1")
dd <- socket.ReceiveAsync (buffer, cts.Token)
xx <- dd.Result.EndOfMessage
Console.WriteLine("HERE 2")
// let res = socket.ReceiveAsync (buffer, cts.Token) |> await

// dd.Wait()
// while dd.Result.EndOfMessage do
// Thread.Sleep(1000)
// Console.WriteLine("{0}", buffer)
printf "%s" (Encoding.ASCII.GetString((Seq.toArray buffer), 0, (dd.Result.Count)))

socket.CloseAsync (WebSocketCloseStatus.Empty, "", cts.Token) |> ignore