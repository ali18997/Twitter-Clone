open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor
open System

//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()

type tweet(sender, tweet, mentions, hashtags) = 
    inherit Object()
    let mutable sender: String = sender
    let mutable tweet: String = tweet
    let mentions: List<String> = mentions
    let hashtags: List<String> = hashtags
    member x.getSender = sender
    member x.getTweet = tweet
    member x.getMentions = mentions
    member x.getHashtags = hashtags

type serverMessage(command, payload) = 
    let mutable command: String = command
    let mutable payload: Object = payload
    member x.getCommand = command
    member x.getPayload = payload

type clientMessage(controlFlag, command, payload) = 
    let mutable controlFlag: Boolean = controlFlag
    let mutable command: String = command
    let mutable payload: Object = payload
    member x.getControlFlag = controlFlag
    member x.getCommand = command
    member x.getPayload = payload

type subscribedTo(client, subscribedClients) =
    let mutable client: String = client
    let mutable subscribedClients: List<String> = subscribedClients
    member x.getClient = client
    member x.getSubscribedClients = subscribedClients
    member x.addSubscribedClients(newClient) = subscribedClients @ [newClient]

type query(typeOf, matching) =
    let mutable typeOf: String = typeOf
    let mutable matching: String = matching
    member x.getTypeOf = typeOf
    member x.getMatching = matching

let Twitter = system.ActorSelection("akka://system/user/Twitter")

let mutable tweets:List<tweet> = []
let mutable subscribedData:List<subscribedTo> = []

let clientAction clientRef controlFlag command payload =
    let clientMsg = new clientMessage(controlFlag, command, payload)
    clientRef <! clientMsg

let clientRetweet client tweet = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction Client true "Retweet" tweet

let clientTweet sender tweet mentions hashtags = 
    let tweetMsg = new tweet(sender, tweet, mentions, hashtags)
    let client =  system.ActorSelection("akka://system/user/"+  sender )
    clientAction client true "Send Tweet" tweetMsg

let clientSubscribe client subscribeTo = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction Client true "Subscribe" subscribedTo

let clientRegister client = 
    let clientRef = system.ActorSelection("akka://system/user/" + client)
    clientAction clientRef true "Register" null

let sendToServer server command payload=
    let serverMsg = new serverMessage(command, payload)
    server <! serverMsg

//Actor
let server (serverMailbox:Actor<serverMessage>) = 
    //Actor Loop that will process a message on each iteration
    let mutable client: ActorSelection = null
    let mutable clientName: String = null

    let rec serverLoop() = actor {

        //Receive the message
        let! msg = serverMailbox.Receive()
        if msg.getCommand = "Register" then
            clientName <- (string) msg.getPayload
            client <- system.ActorSelection("akka://system/user/"+ clientName )
            clientAction client false "Register" null

        elif msg.getCommand = "Send Tweet" then
            let tweet:tweet = downcast msg.getPayload
            tweets <- tweets @ [tweet]
            printfn "Tweet Sent"
        
        elif msg.getCommand = "Subscribe" then
            let client1 = clientName
            let client2 =(string) msg.getPayload
            let mutable flag = true
            for item in subscribedData do
                if (item.getClient.Equals(client1)) then
                    item.addSubscribedClients(client2) |> ignore
                    flag <- false
            if(flag) then
                let sub = new subscribedTo(client1, [client2])
                subscribedData <- subscribedData @ [sub]
            printfn "Subscribed"

        elif msg.getCommand = "Retweet" then
            let tweet:tweet = downcast msg.getPayload
            let tweet2 = new tweet(clientName, tweet.getTweet, tweet.getMentions, tweet.getHashtags)
            tweets <- tweets @ [tweet2]
            printfn "Retweeted"
          
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

        if msg.getCommand = "Register" then
            spawn system ("serverfor"+(string) msg.getPayload) server |> ignore
            let server = system.ActorSelection("akka://system/user/"+"serverfor"+ (string) msg.getPayload)
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

        if(msg.getControlFlag) then
            if(msg.getCommand = "Register") then
                sendToServer Twitter "Register"ClientMailbox.Self.Path.Name
            elif (msg.getCommand = "Send Tweet" || msg.getCommand = "Subscribe" || msg.getCommand = "Retweet") then
                    ClientToServer server msg.getCommand msg.getPayload
        else 
            if(msg.getCommand = "Register") then
                server <- ClientMailbox.Sender()
                printfn "Registered"
        
        return! ClientLoop()
    }

    //Call to start the actor loop
    ClientLoop()

let clientSpawnRegister client = 
    spawn system client Client |> ignore
    clientRegister client

let delay num = 
    System.Threading.Thread.Sleep(num * 1000)

[<EntryPoint>]
let main argv =
    spawn system "Twitter" TwitterEngine |> ignore

    clientSpawnRegister "client0"
    clientSpawnRegister "client1"
 
    delay 1

    clientTweet "client0" "Hello World" ["client2"; "client3"] ["FirstTweet"; "NewUser"]

    clientSubscribe "client0" "client1"
    clientSubscribe "client0" "client2"
    clientSubscribe "client1" "client2"
    
    delay 1

    clientRetweet "client1" tweets.[0]

    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
