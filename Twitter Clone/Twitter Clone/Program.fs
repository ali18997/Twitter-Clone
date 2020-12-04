open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor
open System
open Newtonsoft.Json


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

type subscribedTo() =
    [<DefaultValue>] val mutable client: String
    [<DefaultValue>] val mutable subscribedClients: List<String>

type query() =
    inherit Object()

    [<DefaultValue>] val mutable typeOf: String
    [<DefaultValue>] val mutable matching: String


let Twitter = system.ActorSelection("akka://system/user/Twitter")

let mutable tweets:List<tweet> = []
let mutable subscribedData:List<subscribedTo> = []

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

let sendToServer server command payload=
    let serverMsg = new serverMessage()
    serverMsg.command <- command
    serverMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(serverMsg)
    server <! json

let sendToServer2 server command payload=
    let serverMsg = new serverMessage()
    serverMsg.command <- command
    serverMsg.payload <- payload
    let json = JsonConvert.SerializeObject(serverMsg)
    server <! json

//Actor
let server (serverMailbox:Actor<_>) = 
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
            let mentions:List<String> = tweet.mentions
            
            let mutable toSend: List<String> = []
            for client in mentions do
                toSend <- [client] @ toSend

            for item in subscribedData do
                let list:List<String> = item.subscribedClients
                for item2 in list do
                    if item2.Equals(tweet.sender) then
                        if checkExists item.client toSend then
                            toSend <- [item.client] @ toSend

            for item3 in toSend do
                let client =  system.ActorSelection("akka://system/user/"+ item3 )
                clientAction client false "Live" tweet
    
        //Receive the message
        let! msg2 = serverMailbox.Receive()
        let msg = JsonConvert.DeserializeObject<serverMessage> msg2
        if msg.command = "Register" then
            clientName <- (string) msg.payload
            client <- system.ActorSelection("akka://system/user/"+ clientName )
            clientAction client false "Register" null

        elif msg.command = "Send Tweet" then
            let tweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)msg.payload)
            tweets <- tweets @ [tweet]
            printfn "Tweet Sent"
            sendLive tweet
        
        elif msg.command = "Subscribe" then
            let client1 = clientName
            let client2 =(string) msg.payload
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

        elif msg.command = "Retweet" then
            let tweet:tweet = downcast msg.payload
            let tweet2 = new tweet()
            tweet2.sender <- clientName
            tweet2.tweet <- tweet.tweet
            tweet2.mentions <- tweet.mentions
            tweet2.hashtags <- tweet.hashtags
            tweets <- tweets @ [tweet2]
            printfn "Retweeted"
            sendLive tweet2

        elif msg.command = "Query" then
            let query:query = downcast msg.payload
            if query.typeOf = "MyMentions" then
                let mutable mentionedTweetList: List<tweet> = []
                for tweet in tweets do
                    let mentions = tweet.mentions
                    for mention in mentions do
                        if mention = clientName then
                            mentionedTweetList <- mentionedTweetList @ [tweet]
                clientAction client false "MyMentions" mentionedTweetList
                
            elif query.typeOf = "Subscribed" then
                let mutable subscribedTweetList: List<tweet> = []
                for item in subscribedData do
                    if item.client = clientName then
                        let subscribedClients:List<String> = item.subscribedClients
                        for subClient in subscribedClients do
                            for tweet in tweets do
                                if tweet.sender = subClient then
                                    subscribedTweetList <- subscribedTweetList @ [tweet]
                clientAction client false "Subscribed" subscribedTweetList

            elif query.typeOf = "Hashtags" then
                let mutable hashtagTweetList: List<tweet> = []
                for item in tweets do
                    let list:List<String> = item.hashtags
                    for hashtag in list do
                        if hashtag = query.matching then
                            hashtagTweetList <- hashtagTweetList @ [item]
                clientAction client false "Hashtags" hashtagTweetList

        return! serverLoop()
    }

    //Call to start the actor loop
    serverLoop()

//Actor
let TwitterEngine (EngineMailbox:Actor<_>) = 
    //Actor Loop that will process a message on each iteration

    let rec EngineLoop() = actor {

        //Receive the message
        let! msg2 = EngineMailbox.Receive()
        let msg = JsonConvert.DeserializeObject<serverMessage>msg2
        if msg.command = "Register" then
            spawn system ("serverfor"+(string) msg.payload) server |> ignore
            let server = system.ActorSelection("akka://system/user/"+"serverfor"+ (string) msg.payload)
            server <! msg2

        
        return! EngineLoop()
    }

    //Call to start the actor loop
    EngineLoop()


    //Actor
let Client (ClientMailbox:Actor<_>) = 
    //Actor Loop that will process a message on each iteration

    let mutable server: IActorRef = null

    let ClientToServer server command payload = 
        if server <> null then 
            sendToServer2 server command payload
        else 
            printfn "Not Registered"

    let rec ClientLoop() = actor {

        //Receive the message
        let! msg2 = ClientMailbox.Receive()
        let msg = JsonConvert.DeserializeObject<clientMessage> msg2

        if(msg.controlFlag) then
            if(msg.command = "Register") then
                sendToServer2 Twitter "Register"ClientMailbox.Self.Path.Name
            elif (msg.command = "Send Tweet" || msg.command = "Subscribe" || 
                    msg.command = "Retweet" || msg.command = "Query") then
                    ClientToServer server msg.command msg.payload
        else
            if(msg.command = "Register") then
                server <- ClientMailbox.Sender()
                printfn "%A Registered" ClientMailbox.Self.Path.Name

            elif(msg.command = "MyMentions") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A My Mentions Received %A" ClientMailbox.Self.Path.Name list
 
            elif(msg.command = "Subscribed") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A Subscribed Tweets Received %A" ClientMailbox.Self.Path.Name list

            elif(msg.command = "Hashtags") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A Hashtags Queried Returned %A" ClientMailbox.Self.Path.Name list

            elif(msg.command = "Live") then 
                let liveTweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)msg.payload)
                printfn "%A Live Tweet Received %A" ClientMailbox.Self.Path.Name liveTweet.tweet


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
