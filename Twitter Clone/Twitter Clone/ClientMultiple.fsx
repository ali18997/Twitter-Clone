#I @"packages"
// #r "nuget: Akka.FSharp" 
// #r "nuget: Akka" 
#r "Akka.FSharp.dll"
#r "Akka.dll"
// #r "System.Configuration.ConfigurationManager.dll"
#r "Newtonsoft.Json.dll"
#r "FsPickler.dll"
#r "FSharp.Core.dll"
#r "DotNetty.Buffers.dll"
#r "DotNetty.Transport.dll"
#r "DotNetty.Common.dll"
#r "Microsoft.Extensions.Logging.Abstractions.dll"
#r "Microsoft.Extensions.Logging.dll"
#r "Akkling.dll"

open Akka
open Akka.FSharp
open System
open Akka.Actor
open System.Threading;  
open System.Text; 
open Newtonsoft.Json
open System.Net.WebSockets

let socket = new ClientWebSocket()
let cts = new CancellationTokenSource()
let uri = Uri("ws://localhost:8080/websocket")

let aa = socket.ConnectAsync(uri, cts.Token)

aa.Wait()



//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()

type tweet() = 
    inherit Object()

    [<DefaultValue>] val mutable sender: String
    [<DefaultValue>] val mutable tweet: String
    [<DefaultValue>] val mutable mentions: List<String>
    [<DefaultValue>] val mutable hashtags: List<String>

type serverMessage() = 
    [<DefaultValue>] val mutable clientName: String
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type clientMessage() = 
    [<DefaultValue>] val mutable name: String
    [<DefaultValue>] val mutable controlFlag: Boolean
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type subscribedTo() =
    [<DefaultValue>] val mutable client: String
    [<DefaultValue>] val mutable subscribedClients: List<String>

type query() =
    inherit Object()

    [<DefaultValue>] val mutable typeOf: String
    [<DefaultValue>] val mutable matching: String


let clientAction clientRef controlFlag command payload =
    let clientMsg = new clientMessage()
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(clientMsg)
    clientRef <! json

let clientAction2 clientRef controlFlag command payload =
    let clientMsg = new clientMessage()
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- payload
    let json = JsonConvert.SerializeObject(clientMsg)
    clientRef <! json

let clientQuery client typeOf matching =
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    let query = new query()
    query.typeOf <- typeOf
    query.matching <- matching
    clientAction Client true "Query" query

let clientRetweet client tweet = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction Client true "Retweet" tweet

let clientTweet sender tweet mentions hashtags = 
    let tweetMsg = new tweet()
    tweetMsg.sender <- sender
    tweetMsg.tweet <- tweet
    tweetMsg.mentions <- mentions
    tweetMsg.hashtags <- hashtags
    let client =  system.ActorSelection("akka://system/user/"+  sender )
    clientAction client true "Send Tweet" tweetMsg

let clientSubscribe client subscribeTo = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction2 Client true "Subscribe" subscribeTo

let clientRegister client = 
    let clientRef = system.ActorSelection("akka://system/user/" + client)
    clientAction clientRef true "Register" null

let sendToServer clientName command payload=
    let serverMsg = new serverMessage()
    serverMsg.clientName <- clientName
    serverMsg.command <- command
    serverMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(serverMsg)
    let a = Encoding.ASCII.GetBytes(json) |> ArraySegment<byte>
    let aaa = socket.SendAsync (a, WebSocketMessageType.Text, true, cts.Token)
    aaa.Wait()

    

let sendToServer2 clientName command payload=
    let serverMsg = new serverMessage()
    serverMsg.clientName <- clientName
    serverMsg.command <- command
    serverMsg.payload <- payload
    let json = JsonConvert.SerializeObject(serverMsg)
    let a = Encoding.ASCII.GetBytes(json) |> ArraySegment<byte>
    let aaa = socket.SendAsync (a, WebSocketMessageType.Text, true, cts.Token)
    aaa.Wait()



    //Actor
let Client (ClientMailbox:Actor<_>) = 
    //Actor Loop that will process a message on each iteration

    let mutable server: IActorRef = null

    let ClientToServer name server command payload = 
        if server <> null then 
            sendToServer2 name command payload
        else 
            printfn "Not Registered"

    let rec ClientLoop() = actor {

        //Receive the message
        let! msg2 = ClientMailbox.Receive()
        let msg = JsonConvert.DeserializeObject<clientMessage> msg2

        if(msg.controlFlag) then
            if(msg.command = "Register") then
                sendToServer2 ClientMailbox.Self.Path.Name "Register"ClientMailbox.Self.Path.Name
            elif (msg.command = "Send Tweet" || msg.command = "Subscribe" || 
                    msg.command = "Retweet" || msg.command = "Query") then
                    ClientToServer ClientMailbox.Self.Path.Name server msg.command msg.payload
        else
            if(msg.command = "Register") then
                server <- ClientMailbox.Sender()
                printfn "%A Registered" ClientMailbox.Self.Path.Name
            elif(msg.command = "Retweet") then
                printfn "%A Retweeted" ClientMailbox.Self.Path.Name
            elif(msg.command = "Subscribed") then
                printfn "%A Subscribed %A" ClientMailbox.Self.Path.Name msg.payload
            elif(msg.command = "Retweet") then
                printfn "%A Retweeted" ClientMailbox.Self.Path.Name
            elif(msg.command = "Send Tweet") then
                printfn "%A Tweet Sent: %A" ClientMailbox.Self.Path.Name msg.payload
            elif(msg.command = "MyMentions") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A My Mentions Received %A" ClientMailbox.Self.Path.Name msg.payload
 
            elif(msg.command = "Subscribed") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A Subscribed Tweets Received %A" ClientMailbox.Self.Path.Name list

            elif(msg.command = "Hashtags") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A Hashtags Queried Returned %A" ClientMailbox.Self.Path.Name list

            elif(msg.command = "Live") then 
                let liveTweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)msg.payload)
                printfn "%A Live Tweet Received %A" ClientMailbox.Self.Path.Name msg.payload


        return! ClientLoop()
    }

    //Call to start the actor loop
    ClientLoop()

let clientSpawnRegister client = 
    spawn system client Client |> ignore
    clientRegister client

let delay num = 
    System.Threading.Thread.Sleep(num * 1000)


let receivefun = async{
    let buffer = WebSocket.CreateClientBuffer(1000, 1000)
    let mutable xx = false
    let mutable dd = null
    while(true) do
        dd <- socket.ReceiveAsync (buffer, cts.Token)
        xx <- dd.Result.EndOfMessage
        let msg = JsonConvert.DeserializeObject<clientMessage> (Encoding.ASCII.GetString((Seq.toArray buffer), 0, (dd.Result.Count)))
        let Client =  system.ActorSelection("akka://system/user/"+  msg.name )
        Client <! (Encoding.ASCII.GetString((Seq.toArray buffer), 0, (dd.Result.Count)))
    }

let startClients = async {
    clientSpawnRegister "client0"
    delay 1
    clientSpawnRegister "client1"
    delay 1
    clientSpawnRegister "client2"
 
    delay 1
    clientSubscribe "client0" "client1"
    delay 1
    clientSubscribe "client0" "client2"
    delay 1
    clientSubscribe "client1" "client2"
    delay 1
    clientSubscribe "client1" "client0"
    delay 1
    clientSubscribe "client2" "client0"
    delay 1

    //clientTweet "client0" "Hello World" [] ["FirstTweet"; "NewUser"]
    clientTweet "client0" "Hello World" ["client1"; "client2"] ["FirstTweet"; "NewUser"]
    delay 1
    clientQuery "client1" "MyMentions" null
    clientSpawnRegister "client3"

    delay 1
}
    


[<EntryPoint>]
let main argv =

    
    [receivefun; startClients]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    

    //clientRetweet "client1" tweets.[0]

    //clientQuery "client1" "MyMentions" null
    //clientQuery "client0" "Subscribed" null
    //clientQuery "client1" "Hashtags" "FirstTweet"

    System.Console.ReadKey() |> ignore

    0 // return an integer exit code