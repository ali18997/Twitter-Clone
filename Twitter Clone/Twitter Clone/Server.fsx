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

open Akka
open Akka.FSharp
open Akka.Actor
open System
open System.Diagnostics


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

type serverMessage(command, payload) = 
    let mutable command: String = command
    let mutable payload: Object = payload

    member x.getCommand = command
    member x.getPayload = payload

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

type subscribedTo(client, subscribedClients) =
    let mutable client: String = client
    let mutable subscribedClients: List<String> = subscribedClients

    member x.getClient = client
    member x.getSubscribedClients = subscribedClients
    member x.addSubscribedClients(newClient) = subscribedClients <- subscribedClients @ [newClient]

type clientMessage(controlFlag, command, payload) = 
    let mutable controlFlag: Boolean = controlFlag
    let mutable command: String = command
    let mutable payload: Object = payload

    member x.getControlFlag = controlFlag
    member x.getCommand = command
    member x.getPayload = payload

let clientAction clientRef controlFlag command payload =
    let clientMsg = new clientMessage(controlFlag, command, payload)
    clientRef <! clientMsg

type query(typeOf, matching) =
    inherit Object()

    let mutable typeOf: String = typeOf
    let mutable matching: String = matching

    member x.getTypeOf = typeOf
    member x.getMatching = matching

let mutable tweets:List<tweet> = []
let mutable subscribedData:List<subscribedTo> = []

//spawn system "Twitter" TwitterEngine |> ignore

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
                let client =  system.ActorSelection("akka.tcp://system@localhost:9002/user/"+item3)
                clientAction client false "Live" tweet
    
        //Receive the message
        let! msg = serverMailbox.Receive()
        if msg.getCommand = "Register" then
            clientName <- (string) msg.getPayload
            client <- system.ActorSelection("akka.tcp://system@localhost:9002/user/"+clientName)
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

let TwitterEngine (EngineMailbox:Actor<string>) = 
    //Actor Loop that will process a message on each iteration

    let rec EngineLoop() = actor {

        //Receive the message
        let! msg = EngineMailbox.Receive()

        printfn "here %A" msg
        //printfn "here2 %A" msg.getCommand
        //printfn "here3 %A" msg.getPayload

        //if msg.getCommand = "Register" then
            
            //spawn system ("serverfor"+(string) msg.getPayload) server |> ignore
            //let server = system.ActorSelection("akka.tcp://system@localhost:9001/user/serverfor"+(string) msg.getPayload)
            //server <! msg
        
        return! EngineLoop()
    }

    //Call to start the actor loop
    EngineLoop()

spawn system "Twitter" TwitterEngine |> ignore

System.Console.ReadKey() |> ignore