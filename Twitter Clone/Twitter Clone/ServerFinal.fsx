
open Suave
open Suave.Operators
open Suave.Filters
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open System
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Newtonsoft.Json
open Akka
open Akka.FSharp
open Akka.Actor

//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()
let Twitter = system.ActorSelection("akka://system/user/Twitter")

let mutable flag:Boolean = false
let mutable reply:String = ""


let ws (webSocket : WebSocket) (context: HttpContext) =

  socket {
    // if `loop` is set to false, the server will stop receiving messages
    let mutable loop = true
    while loop do
      // the server will wait for a message to be received without blocking the thread
      let! msg = webSocket.read()

      match msg with
      // the message has type (Opcode * byte [] * bool)
      //
      // Opcode type:
      //   type Opcode = Continuation | Text | Binary | Reserved | Close | Ping | Pong
      //
      // byte [] contains the actual message
      //
      // the last element is the FIN byte, explained later
      | (Text, data, true) ->
        // the message can be converted to a string
        let str = UTF8.toString data
        let response = sprintf "response to %s" str

        printfn "server received %s" str

        // the response needs to be converted to a ByteSegment
        let byteResponse =
          response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment


        Twitter <! str
        // the `send` function sends a message back to the client
        System.Threading.Thread.Sleep(50)
        
        while(flag = true) do
            printfn "server Sending %A" reply 
            let a = System.Text.Encoding.ASCII.GetBytes(reply) |> ByteSegment
            do! webSocket.send Text a true
            flag <- false
            System.Threading.Thread.Sleep(50)

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        // after sending a Close message, stop the loop
        loop <- false

      | _ -> ()
    }

/// An example of explictly fetching websocket errors and handling them in your codebase.
let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
   
   let exampleDisposableResource = { new IDisposable with member __.Dispose() = printfn "Resource needed by websocket connection disposed" }
   let websocketWorkflow = ws webSocket context
   
   async {
    let! successOrError = websocketWorkflow
    match successOrError with
    // Success case
    | Choice1Of2() -> ()
    // Error case
    | Choice2Of2(error) ->
        // Example error handling logic here
        printfn "Error: [%A]" error
        exampleDisposableResource.Dispose()
        
    return successOrError
   }

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    path "/websocketWithError" >=> handShake wsWithErrorHandling
    GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
    NOT_FOUND "Found no handlers." ]


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


let mutable tweets:List<tweet> = []
let mutable subscribedData:List<subscribedTo> = []

let clientAction clientName controlFlag command payload =
    let clientMsg = new clientMessage()
    clientMsg.name <- clientName
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(clientMsg)
    let a = 0
    while (flag) do
        a = 1+1   

    flag <- true
    reply <- json

let clientAction2 clientName controlFlag command payload =
    let clientMsg = new clientMessage()
    clientMsg.name <- clientName
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- payload
    let json = JsonConvert.SerializeObject(clientMsg)
    let a = 0
    while (flag) do
        a = 1+1   

    flag <- true
    reply <- json

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

                clientAction item3 false "Live" tweet

    
        //Receive the message
        let! msg2 = serverMailbox.Receive()
        let msg = JsonConvert.DeserializeObject<serverMessage> msg2
        if msg.command = "Register" then
            clientName <- (string) msg.payload
            client <- system.ActorSelection("akka://system/user/"+ clientName )
            clientAction clientName false "Register" null

        elif msg.command = "Send Tweet" then
            let tweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)msg.payload)
            tweets <- tweets @ [tweet]
            clientAction clientName false "Send Tweet" tweet
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
            clientAction2 clientName false "Subscribed" ((string)msg.payload)

        elif msg.command = "Retweet" then
            let tweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)msg.payload)
            let tweet2 = new tweet()
            tweet2.sender <- clientName
            tweet2.tweet <- tweet.tweet
            tweet2.mentions <- tweet.mentions
            tweet2.hashtags <- tweet.hashtags
            tweets <- tweets @ [tweet2]
            clientAction clientName false "Retweet" null
            sendLive tweet2

        elif msg.command = "Query" then
            let query:query = JsonConvert.DeserializeObject<query> ((string)msg.payload)
            if query.typeOf = "MyMentions" then
                let mutable mentionedTweetList: List<tweet> = []
                for tweet in tweets do
                    let mentions = tweet.mentions
                    for mention in mentions do
                        if mention = clientName then
                            mentionedTweetList <- mentionedTweetList @ [tweet]
                clientAction clientName false "MyMentions" mentionedTweetList
                
            elif query.typeOf = "Subscribed" then
                let mutable subscribedTweetList: List<tweet> = []
                for item in subscribedData do
                    if item.client = clientName then
                        let subscribedClients:List<String> = item.subscribedClients
                        for subClient in subscribedClients do
                            for tweet in tweets do
                                if tweet.sender = subClient then
                                    subscribedTweetList <- subscribedTweetList @ [tweet]
                clientAction clientName false "Subscribed" subscribedTweetList

            elif query.typeOf = "Hashtags" then
                let mutable hashtagTweetList: List<tweet> = []
                for item in tweets do
                    let list:List<String> = item.hashtags
                    for hashtag in list do
                        if hashtag = query.matching then
                            hashtagTweetList <- hashtagTweetList @ [item]
                clientAction clientName false "Hashtags" hashtagTweetList

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
        else 
            let server = system.ActorSelection("akka://system/user/"+"serverfor"+ (string) msg.clientName)
            server <! msg2

        
        return! EngineLoop()
    }

    //Call to start the actor loop
    EngineLoop()




spawn system "Twitter" TwitterEngine |> ignore

   
startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app

System.Console.ReadKey() |> ignore

0 // return an integer exit code