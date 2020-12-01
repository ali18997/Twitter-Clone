#I @"packages"
// #r "nuget: Akka.FSharp" 
// #r "nuget: Akka" 
#r "Akka.FSharp.dll"
#r "Akka.dll"
// #r "System.Configuration.ConfigurationManager.dll"
#r "Newtonsoft.Json.dll"
#r "FsPickler.dll"
#r "FSharp.Core.dll"

open Akka
open Akka.FSharp
open Akka.Actor
open System
open System.Diagnostics

let system = System.create "system" <| Configuration.load ()


type ProcessorMessage = 
    | InitializeClientCoorinator of int
    | RegisterAndSubscribe of bool
    | Operate of bool
    | UpdateConnections of bool
    | Terminate of bool

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

let rec take n list = 
  match n with
  | 0 -> []
  | _ -> List.head list :: take (n - 1) (List.tail list)

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

//let mutable liveClients = List.empty
let mutable coordRef = null
let mutable liveClients = Set.empty

let constructStringFromTweet (tweet:tweet) = 
    let mutable s = String.Empty
    s<-s+tweet.getTweet
    for i in tweet.getMentions do
        s<-s+" "+i
    for i in tweet.getHashtags do
        s<-s+" "+i
    s
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
                let mutable ll = List.map (constructStringFromTweet) list
                if ll.Length>10 then
                    ll<-take 10 ll
                printfn "%A My Mentions Received %A" ClientMailbox.Self.Path.Name ll
 
            elif(msg.getCommand = "Subscribed") then
                let list:List<tweet> = downcast msg.getPayload
                let mutable ll = List.map (constructStringFromTweet) list
                if ll.Length>10 then
                    ll<-take 10 ll
                printfn "%A Subscribed Tweets Received %A" ClientMailbox.Self.Path.Name ll

            elif(msg.getCommand = "Hashtags") then
                let list:List<tweet> = downcast msg.getPayload
                let mutable ll = List.map (constructStringFromTweet) list
                if ll.Length>10 then
                    ll<-take 10 ll
                printfn "%A Hashtags Queried Returned %A" ClientMailbox.Self.Path.Name ll

            elif(msg.getCommand = "Live") then 
                if (Set.contains ((int) ClientMailbox.Self.Path.Name) liveClients) then
                    let liveTweet:tweet = downcast msg.getPayload
                    let str = constructStringFromTweet liveTweet
                    printfn "%s Live Tweet Received %s" ClientMailbox.Self.Path.Name str


        return! ClientLoop()
    }

    //Call to start the actor loop
    ClientLoop()

let clientSpawnRegister client = 
    spawn system client Client |> ignore
    clientRegister client

let delay num = 
    System.Threading.Thread.Sleep(num * 1000)

let getRandArrElement =
  let rnd = Random()
  fun (arr : int list) -> arr.[rnd.Next(arr.Length)]

type System.Random with
    /// Generates an infinite sequence of random numbers within the given range.
    member this.GetValues(minValue, maxValue) =
        Seq.initInfinite (fun _ -> this.Next(minValue, maxValue))

// let r = System.Random()
// let nums = r.GetValues(1, 1000) |> Seq.take 10
// printfn "%A" nums

// let random = System.Random()
// let x = random.Next(1,6)
// printfn "%d" x

let generateRandomSubSet min max sizeOfSubset =  
    let r = System.Random()
    let nums = r.GetValues(min, max) |> Seq.take sizeOfSubset
    Set.ofSeq nums

// let ll = generateRandomSubSet 1 5 2 
// printfn "%A" ll

let generateRandomNumber x y = 
    let random = System.Random()
    random.Next(x,y+1)

let mutable cycle = 0

let ClientCooridnator (mailbox: Actor<_>) =
    let mutable noOfClients = 0
    //let mutable liveClients = Set.empty
    let mutable clientList = List.empty

    let tweetString = "Twitter is an American microblogging and social networking service on which users post and interact with messages known as tweets."
    let hashTagList = ["#twitter";"F#";"#DOS";"#covid19";"#lockdown";"#Akka"]
    //let m = system.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromMilliseconds(5000.0),TimeSpan.FromMilliseconds(5000.0),mailbox.Self,UpdateConnections(true),mailbox.Self)
    let t = system.Scheduler.ScheduleTellOnceCancelable(TimeSpan.FromSeconds(10.0),mailbox.Self,Terminate(true),mailbox.Self)
    let rec loop () = actor {

        let! message = mailbox.Receive ()
        match message with
        | InitializeClientCoorinator(numClients) -> 
            noOfClients <- numClients
            clientList <- List.ofSeq [1..noOfClients]
            mailbox.Self <! RegisterAndSubscribe(true)

        | RegisterAndSubscribe(bool) ->
            //Register all clients
            for i=1 to noOfClients do
                clientSpawnRegister ((string) i)

            delay 2
            //Subscription as per zipf distribution
            let nValue = noOfClients - 1 

            for i=1 to noOfClients do
                let noOfSubscribers = nValue/i
                if (noOfSubscribers<>0) then
                    //let r = System.Random()
                    //let nums = r.GetValues(1, noOfClients+1) |> Seq.take noOfSubscribers
                    let nums = generateRandomSubSet 1 (noOfClients+1) noOfSubscribers
                    printfn "Client %d has subscribers %A" i nums
                    for j in nums do
                        if (j<>i) then
                            clientSubscribe ((string) j) ((string) i)
            delay 2
            //cycle<-cycle+1
            mailbox.Self <! UpdateConnections(true)
            mailbox.Self <! Operate(bool)

        | Operate(bool) ->
            cycle<-cycle+1
            let choices = [1;3]
            //printfn "Live clients = %A" liveClients
            for onlineClient in liveClients do
                //printfn "Online client = %d" onlineClient
                let rank = (noOfClients-1)/onlineClient
                if rank<>0 then
                    for j=1 to rank do
                        let randomChoice = getRandArrElement choices
                        if randomChoice=1 then 
                            let noOfMentions = generateRandomNumber 1 3
                            let mentions = generateRandomSubSet 1 (noOfClients+1) noOfMentions

                            //printfn "Mentions:%A" mentions

                            let list2 = mentions |> Set.map (fun x -> "@" + x.ToString());

                            //printfn "0:%d" (hashTagList.Length-1)
                            let hIndex = generateRandomNumber 0 (hashTagList.Length-1)
                            //printfn "hIndex:%d" hIndex

                            let index = generateRandomNumber 0 (tweetString.Length-1)
                            //printfn "index:%d" index

                            let tweetStr = tweetString.Substring(index)
                            clientTweet ((string) onlineClient) tweetStr (Set.toList list2) [hashTagList.[hIndex]]
                        else if randomChoice=2 then
                            //printfn "i am here" 
                            if (not (tweets.IsEmpty)) then    
                                let ri = generateRandomNumber 0 (tweets.Length-1)
                                //printfn "index is %d" ri
                                clientRetweet ((string) onlineClient) tweets.[ri]
                        else
                            let queryChoice = generateRandomNumber 1 3
                            if queryChoice=1 then
                                clientQuery ((string) onlineClient) "MyMentions" null
                            else if queryChoice=2 then
                                clientQuery ((string) onlineClient) "Subscribed" null
                            else
                                let hIndex = generateRandomNumber 0 (hashTagList.Length-1)
                                clientQuery ((string) onlineClient) "Hashtags" (hashTagList.Item(hIndex))

            mailbox.Self <! UpdateConnections(true)         
            mailbox.Self <! Operate(bool)

        | UpdateConnections(bool) ->
            let no = generateRandomNumber 1 noOfClients
            liveClients <- generateRandomSubSet 1 (noOfClients+1) no

        | Terminate(bool) ->
            //m.Cancel()
            printfn "Cycles = %d" cycle
            t.Cancel()
            printfn "Simulation Completed. Press Any key to close"
            system.Terminate() |> ignore

        return! loop()
    }
    loop()

// let set1 = Set.ofSeq [ 1..30]
// printfn "%A" set1

spawn system "Twitter" TwitterEngine |> ignore

let cc = spawn system "CC" ClientCooridnator
coordRef <- cc
cc<!InitializeClientCoorinator(1000)

System.Console.ReadKey() |> ignore

// clientSpawnRegister "client0"
// //delay 1
// clientSpawnRegister "client1"
// //delay 1
// clientSpawnRegister "client2"

// delay 1
// clientSubscribe "client0" "client1"
// //delay 1
// clientSubscribe "client0" "client2"
// //delay 1
// clientSubscribe "client1" "client2"
// //delay 1
// clientSubscribe "client1" "client0"
// //delay 1
// clientSubscribe "client2" "client0"
// delay 1

// //clientTweet "client0" "Hello World" [] ["FirstTweet"; "NewUser"]
// clientTweet "client0" "Hello World" ["client1"; "client2"] ["FirstTweet"; "NewUser"]
// clientQuery "client1" "Hashtags" "FirstTweet"
// delay 1