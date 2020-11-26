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
    member x.addSubscribedClients(newClient) = subscribedClients <- subscribedClients @ [newClient]

type query(typeOf, matching) =
    inherit Object()

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

let clientQuery client typeOf matching =
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    let query = new query(typeOf, matching)
    clientAction Client true "Query" query

let clientRetweet client tweet = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction Client true "Retweet" tweet

let clientTweet sender tweet mentions hashtags = 
    let tweetMsg = new tweet(sender, tweet, mentions, hashtags)
    let client =  system.ActorSelection("akka://system/user/"+  sender )
    clientAction client true "Send Tweet" tweetMsg

let clientSubscribe client subscribeTo = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction Client true "Subscribe" subscribeTo

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
        
        let sendLive tweet =
        
            let checkExists client toSend = 
                let client:String = client
                let toSend:List<String> = toSend
                let mutable flag = true
                for item in toSend do
                    if item.Equals(client) then
                        flag <- false
                flag

            let tweet:tweet = tweet
            let mentions:List<String> = tweet.getMentions
            
            let mutable toSend: List<String> = []
            for client in mentions do
                toSend <- [client] @ toSend

            for item in subscribedData do
                let list:List<String> = item.getSubscribedClients
                for item2 in list do
                    if item2.Equals(tweet.getSender) then
                        if checkExists item.getClient toSend then
                            toSend <- [item.getClient] @ toSend

            for item3 in toSend do
                let client =  system.ActorSelection("akka://system/user/"+ item3 )
                clientAction client false "Live" tweet
    
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
            sendLive tweet
        
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
            sendLive tweet2

        elif msg.getCommand = "Query" then
            let query:query = downcast msg.getPayload
            if query.getTypeOf = "MyMentions" then
                let mutable mentionedTweetList: List<tweet> = []
                for tweet in tweets do
                    let mentions = tweet.getMentions
                    for mention in mentions do
                        if mention = clientName then
                            mentionedTweetList <- mentionedTweetList @ [tweet]
                clientAction client false "MyMentions" mentionedTweetList
                
            elif query.getTypeOf = "Subscribed" then
                let mutable subscribedTweetList: List<tweet> = []
                for item in subscribedData do
                    if item.getClient = clientName then
                        let subscribedClients:List<String> = item.getSubscribedClients
                        for subClient in subscribedClients do
                            for tweet in tweets do
                                if tweet.getSender = subClient then
                                    subscribedTweetList <- subscribedTweetList @ [tweet]
                clientAction client false "Subscribed" subscribedTweetList

            elif query.getTypeOf = "Hashtags" then
                let mutable hashtagTweetList: List<tweet> = []
                for item in tweets do
                    let list:List<String> = item.getHashtags
                    for hashtag in list do
                        if hashtag = query.getMatching then
                            hashtagTweetList <- hashtagTweetList @ [item]
                clientAction client false "Hashtags" hashtagTweetList

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
            elif (msg.getCommand = "Send Tweet" || msg.getCommand = "Subscribe" || 
                    msg.getCommand = "Retweet" || msg.getCommand = "Query") then
                    ClientToServer server msg.getCommand msg.getPayload
        else
            if(msg.getCommand = "Register") then
                server <- ClientMailbox.Sender()
                printfn "%A Registered" ClientMailbox.Self.Path.Name

            elif(msg.getCommand = "MyMentions") then
                let list:List<tweet> = downcast msg.getPayload
                printfn "%A My Mentions Received %A" ClientMailbox.Self.Path.Name list
 
            elif(msg.getCommand = "Subscribed") then
                let list:List<tweet> = downcast msg.getPayload
                printfn "%A Subscribed Tweets Received %A" ClientMailbox.Self.Path.Name list

            elif(msg.getCommand = "Hashtags") then
                let list:List<tweet> = downcast msg.getPayload
                printfn "%A Hashtags Queried Returned %A" ClientMailbox.Self.Path.Name list

            elif(msg.getCommand = "Live") then 
                let liveTweet:tweet = downcast msg.getPayload
                printfn "%A Live Tweet Received %A" ClientMailbox.Self.Path.Name liveTweet


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

    //clientRetweet "client1" tweets.[0]

    //clientQuery "client1" "MyMentions" null
    //clientQuery "client0" "Subscribed" null
    //clientQuery "client1" "Hashtags" "FirstTweet"

    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
