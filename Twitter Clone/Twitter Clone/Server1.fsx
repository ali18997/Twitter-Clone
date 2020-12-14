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

#load @"./Messages.fsx"
#load @"./DataStruc.fsx"

open Akka
open Akka.FSharp
open Akka.Actor
open System
open System.Diagnostics
open Newtonsoft.Json
open Messages
open DataStruc

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.dot-netty.tcp {
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
    let clientMsg = clientMessage(controlFlag, command, payload)
    clientRef <! JsonConvert.SerializeObject(clientMsg)

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
                let client =  system.ActorSelection("akka.tcp://system@localhost:9002/user/"+item3)
                clientAction client false "Live" tweet
    
        //Receive the message
        let! message = serverMailbox.Receive()

        match box message with
        | :? string as m ->
            let received = JsonConvert.DeserializeObject<serverMessage>(m)
            if received.getCommand = "Register" then
                clientName <- (string) received.getPayload
                client <- system.ActorSelection("akka.tcp://system@localhost:9002/user/"+clientName)
                clientAction client false "Register" null
            elif received.getCommand = "Send Tweet" then
                let tweet:tweet = downcast received.getPayload
                tweets <- tweets @ [tweet]
                printfn "Tweet Sent"
                sendLive tweet
        
            elif received.getCommand = "Subscribe" then
                let client1 = clientName
                let client2 =(string) received.getPayload
                let mutable flag = true
                for item in subscribedData do
                    if (item.getClient.Equals(client1)) then
                        item.addSubscribedClients(client2) |> ignore
                        flag <- false
                if(flag) then
                    let sub = new subscribedTo(client1, [client2])
                    subscribedData <- subscribedData @ [sub]
                printfn "Subscribed"

            elif received.getCommand = "Retweet" then
                let tweet:tweet = downcast received.getPayload
                let tweet2 = new tweet(clientName, tweet.getTweet, tweet.getMentions, tweet.getHashtags)
                tweets <- tweets @ [tweet2]
                printfn "Retweeted"
                sendLive tweet2

            elif received.getCommand = "Query" then
                let query:query = downcast received.getPayload
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

                else
                    printfn "Unexpected query"            
            else
                printfn "Unexpected message at server"

        | _ ->
            Console.WriteLine ("Unexpected message at server actor")
        return! serverLoop()
    }

    //Call to start the actor loop
    serverLoop()

let TwitterEngine (EngineMailbox:Actor<_>) = 
    //Actor Loop that will process a message on each iteration

    let rec EngineLoop() = actor {

        //Receive the message
        let! message = EngineMailbox.Receive()

        match box message with

        | :? string as m ->
            let received = JsonConvert.DeserializeObject<serverMessage>(m)

            if received.getCommand="Register" then
                spawn system ("serverfor"+(string) received.getPayload) server |> ignore
                let server = system.ActorSelection("akka.tcp://system@localhost:9001/user/serverfor"+(string) received.getPayload)
                server <! JsonConvert.SerializeObject(received)

        | _ ->
            Console.WriteLine ("Unexpected message at twitter engine actor")
        
        return! EngineLoop()
    }

    //Call to start the actor loop
    EngineLoop()

spawn system "Twitter" TwitterEngine |> ignore

System.Console.ReadKey() |> ignore