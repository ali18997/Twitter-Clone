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

#load @"./Messages2.fsx"
#load @"./DataStruc2.fsx"

open Akka
open Akka.FSharp
open Akka.Actor
open System
open System.Diagnostics
open Newtonsoft.Json
open Messages2
open DataStruc2

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            maximum-payload-bytes = 200000 bytes
            remote.helios.tcp {
                hostname = ""localhost""
                port = 9001
            }
        }"

let system = ActorSystem.Create("system", config)

// type serverMessage(command, payload) = 
//     let mutable command: String = command
//     let mutable payload: Object = payload

//     member x.getCommand = command
//     member x.getPayload = payload

// type tweet(sender, tweet, mentions, hashtags) = 
//     inherit Object()

//     let mutable sender: String = sender
//     let mutable tweet: String = tweet
//     let mentions: List<String> = mentions
//     let hashtags: List<String> = hashtags

//     member x.getSender = sender
//     member x.getTweet = tweet
//     member x.getMentions = mentions
//     member x.getHashtags = hashtags

// type subscribedTo(client, subscribedClients) =
//     let mutable client: String = client
//     let mutable subscribedClients: List<String> = subscribedClients

//     member x.getClient = client
//     member x.getSubscribedClients = subscribedClients
//     member x.addSubscribedClients(newClient) = subscribedClients <- subscribedClients @ [newClient]

// type clientMessage(controlFlag, command, payload) = 
//     let mutable controlFlag: Boolean = controlFlag
//     let mutable command: String = command
//     let mutable payload: Object = payload

//     member x.getControlFlag = controlFlag
//     member x.getCommand = command
//     member x.getPayload = payload

let clientAction clientRef controlFlag command payload =
    let clientMsg = clientMessage()
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(clientMsg)
    clientRef <! json

// type query(typeOf, matching) =
//     inherit Object()

//     let mutable typeOf: String = typeOf
//     let mutable matching: String = matching

//     member x.getTypeOf = typeOf
//     member x.getMatching = matching

let mutable tweets:List<tweet> = []
let mutable subscribedData:List<subscribedTo> = []

//spawn system "Twitter" TwitterEngine |> ignore

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
                let client =  system.ActorSelection("akka.tcp://system@localhost:9002/user/"+item3)
                clientAction client false "Live" tweet
    
        //Receive the message
        let! msg = serverMailbox.Receive()
        let message = JsonConvert.DeserializeObject<serverMessage> msg

        if message.command = "Register" then
            clientName <- JsonConvert.DeserializeObject<String> ((string)message.payload)
            //clientName <- (string) message.payload
            client <- system.ActorSelection("akka.tcp://system@localhost:9002/user/"+clientName)
            clientAction client false "Register" null
        elif message.command = "Send Tweet" then
            let tweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)message.payload)
            tweets <- tweets @ [tweet]
            //printfn "Tweet Sent"
            sendLive tweet
            let clientMsg = new clientMessage()
            clientMsg.command<-"Tweeted"
            clientMsg.controlFlag<-false
            clientMsg.payload<-null
            let json = JsonConvert.SerializeObject(clientMsg)
            serverMailbox.Sender()<!json
        
        elif message.command = "Subscribe" then
            let client1 = clientName
            //let client2 =(string) message.payload
            let client2 = JsonConvert.DeserializeObject<String> ((string)message.payload)
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
            //printfn "Subscribed"

        elif message.command = "Retweet" then
            let tweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)message.payload)
            let tweet2 = new tweet()
            tweet2.sender <- clientName
            tweet2.tweet <- tweet.tweet
            tweet2.mentions <- tweet.mentions
            tweet2.hashtags <- tweet.hashtags
            tweets <- tweets @ [tweet2]
            //printfn "Retweeted"
            sendLive tweet2

        elif message.command = "Query" then
            let query:query = JsonConvert.DeserializeObject<query> ((string)message.payload)
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

            else
                printfn "Unexpected query"            
        else
            printfn "Unexpected message at server"

        return! serverLoop()
    }

    //Call to start the actor loop
    serverLoop()

let TwitterEngine (EngineMailbox:Actor<_>) = 
    //Actor Loop that will process a message on each iteration

    let rec EngineLoop() = actor {

        //Receive the message
        let! msg = EngineMailbox.Receive()
        let message = JsonConvert.DeserializeObject<serverMessage>msg
        //printfn "At twitter engine %s" message.command
        if message.command="Register" then
            //printfn "hello"
            let nn = JsonConvert.DeserializeObject<String> ((string)message.payload)
            spawn system ("serverfor"+nn) server 
            
            //let server = system.ActorSelection("akka.tcp://system@localhost:9001/user/serverfor"+nn)
            let server = system.ActorSelection("akka://system/user/serverfor"+nn)
            //printfn "%A" server
            let json = JsonConvert.SerializeObject(message)
            server <! json
            //printfn "Sent"
        else
            printfn "Unexpected message at twitter engine"
        
        return! EngineLoop()
    }

    //Call to start the actor loop
    EngineLoop()

spawn system "Twitter" TwitterEngine |> ignore

System.Console.ReadKey() |> ignore
