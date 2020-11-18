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


let Twitter = system.ActorSelection("akka://system/user/Twitter")

let mutable tweets:List<tweet> = []

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

    let rec ClientLoop() = actor {

        //Receive the message
        let! msg = ClientMailbox.Receive()

        if(msg.controlFlag) then
            if(msg.command = "Register") then
                let serverMsg = new serverMessage()
                serverMsg.command <- "Register"
                serverMsg.payload <- ClientMailbox.Self.Path.Name
                Twitter <! serverMsg
            elif (msg.command = "Send Tweet") then
                if (server<>null) then 
                    let serverMsg = new serverMessage()
                    serverMsg.command <- "Send Tweet"
                    serverMsg.payload <- msg.payload
                    server <! serverMsg
                else 
                    printfn "Not Registered"
        else 
            if(msg.command = "Register") then
                server <- ClientMailbox.Sender()
        
        return! ClientLoop()
    }

    //Call to start the actor loop
    ClientLoop()



[<EntryPoint>]
let main argv =
    spawn system "Twitter" TwitterEngine |> ignore

    spawn system "client0" Client |> ignore

    let clientRef = system.ActorSelection("akka://system/user/client0")
    
    clientAction clientRef true "Register" null
 
    printf ""

    clientTweet "client0" "Hello World" ["client2"; "client3"] ["FirstTweet"; "NewUser"]

    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
