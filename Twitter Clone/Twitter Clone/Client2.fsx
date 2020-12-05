#I @"packages"
// #r "nuget: Akka.FSharp" 
// #r "nuget: Akka" 
#r "Akka.FSharp.dll"
#r "Akka.dll"
// #r "System.Configuration.ConfigurationManager.dll"
#r "Newtonsoft.Json.dll"
#r "FsPickler.dll"
#r "FSharp.Core.dll"

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
            remote.helios.tcp {
                hostname = ""localhost""
                port = 9002
            }
        }"

let system = ActorSystem.Create("system", config)

// type ProcessorMessage = 
// //    | InitializeClientCoorinator of int
// //    | RegisterAndSubscribe of bool
// //    | Operate of bool
// //    | UpdateConnections of bool
//     | Terminate of bool


// type InitializeClientCoorinator(numberOfClients) = 
//     let command: String = "InitializeClientCoorinator"
//     let noOfClients: int = numberOfClients

// type ClientCoordinatorMessage(command,noOfClients) = 
//     member this.Command = command
//     member this.NoOfClients = noOfClients

// type Terminate() = 
//     let command: String = "Terminate"

// type RegisterAndSubscribe() =
//     let command: String = "RegisterAndSubscribe"

// type Operate() = 
//     let command: String = "Operate"

// type UpdateConnections() = 
//     let command: String = "UpdateConnections"

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

// type serverMessage(command, payload) = 
//     let mutable command: String = command
//     let mutable payload: Object = payload

//     member x.getCommand = command
//     member x.getPayload = payload

// type clientMessage(controlFlag, command, payload) = 
//     let mutable controlFlag: Boolean = controlFlag
//     let mutable command: String = command
//     let mutable payload: Object = payload

//     member x.getControlFlag = controlFlag
//     member x.getCommand = command
//     member x.getPayload = payload


// type wrapper(clientMessage) = 
//     member this.clientMessage = clientMessage

// let stttt = "{\"getControlFlag\":true,\"getCommand\":\"Register\",\"getPayload\":null}"
// printfn "json is %s" stttt

// let kkkkk = "{\"clientMessage\":{\"getControlFlag\":true,\"getCommand\":\"Register\",\"getPayload\":null}}"
// printfn "json is %s" kkkkk

// let abc = clientMessage(true,"register",[1;2;3])
// printfn "proto is %s" (abc.ToString())

// let www = wrapper(abc)
// printfn "www is %A" www

// let xyz = JsonConvert.SerializeObject(www)
// printfn "ser is %s" xyz

// let mmmm = JsonConvert.DeserializeObject<clientMessage>(xyz)
// printfn "obj is %A" mmmm.getPayload

// type subscribedTo(client, subscribedClients) =
//     let mutable client: String = client
//     let mutable subscribedClients: List<String> = subscribedClients

//     member x.getClient = client
//     member x.getSubscribedClients = subscribedClients
//     member x.addSubscribedClients(newClient) = subscribedClients <- subscribedClients @ [newClient]

// type query(typeOf, matching) =
//     inherit Object()

//     let mutable typeOf: String = typeOf
//     let mutable matching: String = matching

//     member x.getTypeOf = typeOf
//     member x.getMatching = matching

let clientAction clientRef controlFlag command payload =
    let clientMsg = clientMessage()
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(clientMsg)
    clientRef <! json

let clientQuery client typeOf matching =
    //let Client =  system.ActorSelection("akka.tcp://system@localhost:9002/user/"+client)
    let Client =  system.ActorSelection("akka://system/user/"+client)
    let query = query()
    query.typeOf <- typeOf
    query.matching <- matching
    clientAction Client true "Query" query

let clientRetweet client tweet = 
    //let Client =  system.ActorSelection("akka.tcp://system@localhost:9002/user/"+client)
    let Client =  system.ActorSelection("akka://system/user/"+client)
    clientAction Client true "Retweet" tweet

let clientTweet sender tweet mentions hashtags = 
    let tweetMsg = new tweet()
    tweetMsg.sender <- sender
    tweetMsg.tweet <- tweet
    tweetMsg.mentions <- mentions
    tweetMsg.hashtags <- hashtags
    //let client =  system.ActorSelection("akka.tcp://system@localhost:9002/user/"+sender)
    let client =  system.ActorSelection("akka://system/user/"+sender)
    clientAction client true "Send Tweet" tweetMsg

let clientSubscribe client subscribeTo = 
    //let Client =  system.ActorSelection("akka.tcp://system@localhost:9002/user/"+client)
    let Client =  system.ActorSelection("akka://system/user/"+client)
    clientAction Client true "Subscribe" subscribeTo

let clientRegister client = 
    //printfn "bbb"
    //let clientRef = system.ActorSelection("akka.tcp://system@localhost:9002/user/"+client)
    //printfn "%A" clientRef
    let clientRef =  system.ActorSelection("akka://system/user/"+client)
    clientAction clientRef true "Register" null

let sendToServer server command payload=
    let serverMsg = serverMessage()
    serverMsg.command <- command
    serverMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(serverMsg)
    server <! json

let constructStringFromTweet (tweet:tweet) = 
    let mutable s = String.Empty
    s<-s+tweet.tweet
    for i in tweet.mentions do
        s<-s+" @"+i
    for i in tweet.hashtags do
        s<-s+" "+i
    s

let rec take n list = 
  match n with
  | 0 -> []
  | _ -> List.head list :: take (n - 1) (List.tail list)

let delay num = 
    System.Threading.Thread.Sleep(num * 1000)

let getRandArrElement =
  let rnd = Random()
  fun (arr : int list) -> arr.[rnd.Next(arr.Length)]

type System.Random with
    /// Generates an infinite sequence of random numbers within the given range.
    member this.GetValues(minValue, maxValue) =
        Seq.initInfinite (fun _ -> this.Next(minValue, maxValue))

let generateRandomSubSet min max sizeOfSubset =  
    let r = System.Random()
    let nums = r.GetValues(min, max) |> Seq.take sizeOfSubset
    Set.ofSeq nums

let generateRandomNumber x y = 
    let random = System.Random()
    random.Next(x,y+1)



let Twitter = system.ActorSelection("akka.tcp://system@localhost:9001/user/Twitter")
let mutable liveClients = Set.empty

let Client (ClientMailbox:Actor<_>) = 
    //Actor Loop that will process a message on each iteration

    let mutable server: IActorRef = null
    let mutable tweetList:List<tweet> = List.empty

    let ClientToServer server command payload = 
        if server <> null then 
            sendToServer server command payload
        else 
            printfn "Not Registered"

    let rec ClientLoop() = actor {

        //Receive the message
        let! msg = ClientMailbox.Receive()
        let message = JsonConvert.DeserializeObject<clientMessage> msg

        if message.controlFlag then 
            if message.command="Register" then
                printfn "%s" ClientMailbox.Self.Path.Name
                sendToServer Twitter "Register" ClientMailbox.Self.Path.Name
            elif (message.command = "Send Tweet" || message.command = "Subscribe" || message.command = "Query") then
                //let query:query = JsonConvert.DeserializeObject<payload> ((string)message.payload)
                let oobj = JsonConvert.DeserializeObject ((string)message.payload)
                //ClientToServer server message.command message.payload
                ClientToServer server message.command oobj
            else
                if(not (tweetList.IsEmpty)) then 
                    let randomIndex = generateRandomNumber 0 (tweetList.Length-1)
                    ClientToServer server message.command tweetList.[randomIndex]
        else
            if(message.command = "Register") then
                server <- ClientMailbox.Sender()
                printfn "%A Registered" ClientMailbox.Self.Path.Name

            elif(message.command = "MyMentions") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)message.payload)
                let mutable ll = List.map (constructStringFromTweet) list
                if ll.Length>10 then
                    ll<-take 10 ll
                printfn "%A My Mentions Received %A" ClientMailbox.Self.Path.Name ll
 
            elif(message.command = "Subscribed") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)message.payload)
                let mutable ll = List.map (constructStringFromTweet) list
                if ll.Length>10 then
                    ll<-take 10 ll
                printfn "%A Subscribed Tweets Received %A" ClientMailbox.Self.Path.Name ll

            elif(message.command = "Hashtags") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)message.payload)
                let mutable ll = List.map (constructStringFromTweet) list
                if ll.Length>10 then
                    ll<-take 10 ll
                printfn "%A Hashtags Queried Returned %A" ClientMailbox.Self.Path.Name ll

            elif(message.command = "Live") then
                let liveTweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)message.payload)
                tweetList<-tweetList @ [liveTweet] 
                if (Set.contains ((int) ClientMailbox.Self.Path.Name) liveClients) then
                    let str = constructStringFromTweet liveTweet
                    printfn "%s Live Tweet Received %s" ClientMailbox.Self.Path.Name str
            
            else
                printfn "Unexpected client message in Client Actor"

        return! ClientLoop()
    }

    //Call to start the actor loop
    ClientLoop()

let clientSpawnRegister client = 
    //printfn "aaa"
    spawn system client Client |> ignore
    clientRegister client

let ClientCoordinator (mailbox: Actor<_>) =
    let mutable noOfClients = 0
    //let mutable liveClients = Set.empty
    let mutable clientList = List.empty

    let tweetString = "Twitter is an American microblogging and social networking service on which users post and interact with messages known as tweets."
    let hashTagList = ["#twitter";"F#";"#DOS";"#covid19";"#lockdown";"#Akka"]
    //let m = system.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromMilliseconds(5000.0),TimeSpan.FromMilliseconds(5000.0),mailbox.Self,UpdateConnections(true),mailbox.Self)
    //let t = system.Scheduler.ScheduleTellOnceCancelable(TimeSpan.FromSeconds(10.0),mailbox.Self,ClientCoordinatorMessage("Terminate",noOfClients),mailbox.Self)
    let rec loop () = actor {

        let! msg = mailbox.Receive ()
        let message = JsonConvert.DeserializeObject<ClientCoordinatorMessage> msg

        if message.Command="InitializeClientCoorinator" then
            noOfClients <- message.NoOfClients
            clientList <- List.ofSeq [1..noOfClients]
            let clientCoorMsg = new ClientCoordinatorMessage()
            clientCoorMsg.Command <- "RegisterAndSubscribe"
            clientCoorMsg.NoOfClients <- noOfClients
            let json = JsonConvert.SerializeObject(clientCoorMsg)
            mailbox.Self <! json
        elif message.Command="RegisterAndSubscribe" then
            printfn "xxx"
            for i=1 to noOfClients do
                clientSpawnRegister ((string) i)
            //printfn "regis %d" i

            delay 2
            //Subscription as per zipf distribution
            let nValue = noOfClients - 1 

            for i=1 to noOfClients do
                let noOfSubscribers = nValue/i
                if (noOfSubscribers<>0) then
                    //let r = System.Random()
                    //let nums = r.GetValues(1, noOfClients+1) |> Seq.take noOfSubscribers
                    let nums = generateRandomSubSet 1 (noOfClients+1) noOfSubscribers
                    //printfn "Client %d has subscribers %A" i nums
                    for j in nums do
                        if (j<>i) then
                            clientSubscribe ((string) j) ((string) i)
            delay 2
            //cycle<-cycle+1
            let clientCoorMsg1 = new ClientCoordinatorMessage()
            clientCoorMsg1.Command <- "UpdateConnections"
            clientCoorMsg1.NoOfClients <- noOfClients
            let json = JsonConvert.SerializeObject(clientCoorMsg1)
            mailbox.Self <! json

            let clientCoorMsg2 = new ClientCoordinatorMessage()
            clientCoorMsg2.Command <- "Operate"
            clientCoorMsg2.NoOfClients <- noOfClients
            let json = JsonConvert.SerializeObject(clientCoorMsg2)
            mailbox.Self <! json
        elif message.Command="Operate" then
            let choices = [1;2;3]
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

                            let list2 = mentions |> Set.map (fun x -> x.ToString());

                            //printfn "0:%d" (hashTagList.Length-1)
                            let hIndex = generateRandomNumber 0 (hashTagList.Length-1)
                            //printfn "hIndex:%d" hIndex

                            let index = generateRandomNumber 0 (tweetString.Length-1)
                            //printfn "index:%d" index

                            let tweetStr = tweetString.Substring(index)
                            clientTweet ((string) onlineClient) tweetStr (Set.toList list2) [hashTagList.[hIndex]]
                        else if randomChoice=2 then
                            //printfn "i am here" 
                            // if (not (tweets.IsEmpty)) then    
                            //     let ri = generateRandomNumber 0 (tweets.Length-1)
                            //     //printfn "index is %d" ri
                            clientRetweet ((string) onlineClient) null

                        else
                            let queryChoice = generateRandomNumber 1 3
                            if queryChoice=1 then
                                clientQuery ((string) onlineClient) "MyMentions" null
                            else if queryChoice=2 then
                                clientQuery ((string) onlineClient) "Subscribed" null
                            else
                                let hIndex = generateRandomNumber 0 (hashTagList.Length-1)
                                clientQuery ((string) onlineClient) "Hashtags" (hashTagList.Item(hIndex))
            
            delay 5
            
            let clientCoorMsg1 = new ClientCoordinatorMessage()
            clientCoorMsg1.Command <- "UpdateConnections"
            clientCoorMsg1.NoOfClients <- noOfClients
            let json = JsonConvert.SerializeObject(clientCoorMsg1)
            mailbox.Self <! json

            let clientCoorMsg2 = new ClientCoordinatorMessage()
            clientCoorMsg2.Command <- "Operate"
            clientCoorMsg2.NoOfClients <- noOfClients
            let json = JsonConvert.SerializeObject(clientCoorMsg2)
            mailbox.Self <! json
        elif message.Command="UpdateConnections" then
            let no = generateRandomNumber 1 noOfClients
            liveClients <- generateRandomSubSet 1 (noOfClients+1) no
        elif message.Command="Terminate" then
            //t.Cancel()
            printfn "Simulation Completed. Press Any key to close"
            system.Terminate() |> ignore
        else
            printfn "Crap"

        return! loop()
    }
    loop()

// let rec server = function
// | Message(num) ->
//     printfn "Got a number %d" num
//     become server

// let cc = spawn system "CC" <| props(actorOf ClientCoordinator)

let cc = spawn system "CC" ClientCoordinator
let clientCoorMsg = new ClientCoordinatorMessage()
clientCoorMsg.Command <- "InitializeClientCoorinator"
clientCoorMsg.NoOfClients <- 2000
let json = JsonConvert.SerializeObject(clientCoorMsg)
cc<!json

System.Console.ReadKey() |> ignore