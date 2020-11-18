open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor
open System

//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()

type tweet() = 
    inherit Object()
    [<DefaultValue>] val mutable sender: String
    [<DefaultValue>] val mutable tweet: String
    [<DefaultValue>] val mutable mentions: List<String>
    [<DefaultValue>] val mutable hashtags: List<String>

type serverMessage() = 
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type clientMessage() = 
    [<DefaultValue>] val mutable controlFlag: Boolean
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type subscribedTo()=
    [<DefaultValue>] val mutable client: String
    [<DefaultValue>] val mutable subscribedClients: List<String>


let Twitter = system.ActorSelection("akka://system/user/Twitter")

let mutable tweets:List<tweet> = []
let mutable subscribedData:List<subscribedTo> = []

let clientAction clientRef controlFlag command payload =
    let clientMsg = new clientMessage()
    clientMsg.command <- command
    clientMsg.controlFlag <- controlFlag
    clientMsg.payload <- payload
    clientRef <! clientMsg

let clientTweet sender tweet mentions hashtags = 
    let tweet:String = tweet
    let mentions: List<String> = mentions
    let hashtags: List<String> = hashtags
    let sender:String = sender
    let tweetMsg = new tweet()
    tweetMsg.tweet <- tweet
    tweetMsg.hashtags <- hashtags
    tweetMsg.mentions <- mentions
    tweetMsg.sender <- sender
    let client =  system.ActorSelection("akka://system/user/"+  sender )
    clientAction client true "Send Tweet" tweetMsg

let clientSubscribe client subscribeTo = 
    let client:String = client
    let subscribedTo:String = subscribeTo
    let list = [client; subscribedTo]
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction Client true "Subscribe" list

let clientRegister client = 
    let clientRef = system.ActorSelection("akka://system/user/" + client)
    clientAction clientRef true "Register" null

let sendToServer server command payload=
    let serverMsg = new serverMessage()
    serverMsg.command <- command
    serverMsg.payload <- payload
    server <! serverMsg

//Actor
let server (serverMailbox:Actor<serverMessage>) = 
    //Actor Loop that will process a message on each iteration
    let mutable client: ActorSelection = null

    let rec serverLoop() = actor {

        //Receive the message
        let! msg = serverMailbox.Receive()
        if msg.command = "Register" then
            client <- system.ActorSelection("akka://system/user/"+ (string) msg.payload )
            clientAction client false "Register" null

        elif msg.command = "Send Tweet" then
            let tweet:tweet = downcast msg.payload
            tweets <- tweets @ [tweet]
            printfn "Tweet Sent"
        
        elif msg.command = "Subscribe" then
            let list:List<String> =  msg.payload:?>List<String>
            let client1 = list.[0]
            let client2 = list.[1]
            let mutable flag = true
            for item in subscribedData do
                if (item.client.Equals(client1)) then
                    item.subscribedClients <- item.subscribedClients @ [client2]
                    flag <- false
            if(flag) then
                let sub = new subscribedTo()
                sub.client <- client1
                sub.subscribedClients <- [client2]
                subscribedData <- subscribedData @ [sub]
            printfn "Subscribed"
           

        return! serverLoop()
    }

    //Call to start the actor loop
    serverLoop()

//Actor
let TwitterEngine (EngineMailbox:Actor<serverMessage>) = 
    //Actor Loop that will process a message on each iteration

    let rec EngineLoop() = actor {

        //Receive the message
        let! msg = EngineMailbox.Receive()

        if msg.command = "Register" then
            spawn system ("serverfor"+(string) msg.payload) server |> ignore
            let server = system.ActorSelection("akka://system/user/"+"serverfor"+ (string) msg.payload)
            server <! msg
        
        return! EngineLoop()
    }

    //Call to start the actor loop
    EngineLoop()


    //Actor
let Client (ClientMailbox:Actor<clientMessage>) = 
    //Actor Loop that will process a message on each iteration

    let mutable server: IActorRef = null

    let ClientToServer server command payload = 
        if server <> null then 
            sendToServer server command payload
        else 
            printfn "Not Registered"

    let rec ClientLoop() = actor {

        //Receive the message
        let! msg = ClientMailbox.Receive()

        if(msg.controlFlag) then
            if(msg.command = "Register") then
                sendToServer Twitter "Register"ClientMailbox.Self.Path.Name
            elif (msg.command = "Send Tweet" || msg.command = "Subscribe") then
                    ClientToServer server msg.command msg.payload
        else 
            if(msg.command = "Register") then
                server <- ClientMailbox.Sender()
                printfn "Registered"
        
        return! ClientLoop()
    }

    //Call to start the actor loop
    ClientLoop()

let clientSpawnRegister client = 
    spawn system client Client |> ignore
    clientRegister client

[<EntryPoint>]
let main argv =
    spawn system "Twitter" TwitterEngine |> ignore

    clientSpawnRegister "client0"
    clientSpawnRegister "client1"
 
    printf ""

    clientTweet "client0" "Hello World" ["client2"; "client3"] ["FirstTweet"; "NewUser"]

    clientSubscribe "client0" "client1"
    clientSubscribe "client0" "client2"
    clientSubscribe "client1" "client2"

    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
