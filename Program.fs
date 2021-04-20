//module Suave

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open Akka
open Akka.Actor
open System.Collections.Generic
open Akka.FSharp
open System.Security.Cryptography
open System
open System.Net
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open System.Threading





type tweetStorageUser = struct
    val tweetId: int
    val tweet: String
    val creator: int
    val isRetweet: bool
    val retweetId: int
    val sharedBy: int
  
    new (tid,msg,cid,isR,rtid,sid) = {tweetId= tid;tweet = msg;creator = cid;isRetweet = isR; retweetId = rtid;sharedBy = sid}
end


type userDetails = struct
    val userId: int
    val FirstName: String
    val LastName: String
    val username: String
    val Email: String
    val Password: String
  
    new (uid,fn,ln,un,email,pwd) = {userId= uid;FirstName = fn; LastName = ln; username = un;Email = email;Password =pwd}
end
  
type simulatorMessage = 
    | Start of int
    | GetAllUser of Map<int,IActorRef>
    | SimulateConnections
    | SimulateTweet of String*int
    | SimulateReTweet of int*int
    | SearchFromClient of String*int
    | SwitchSimulator of int
    | TweetByAll of String
    | ReTweetByAll 
    | CountTweetByAll of int
    | Reset
    | DisplayFollowerClient of int
    | DisplayFollowerServer of int
    | DisplayFollowingClient of int
    | DisplayFollowingServer of int
    | DisplayHT of int
  
type clientGeneratorMessage = 
    | SpawnClient of int
  
type clientMessage = 
    | InitializeClient of int
    | PopulateFollowers of Set<int>
    | PopulateFollowing of Set<int>
    | AddFollower of int
    | AddFollowing of int
    | GenerateTweet of String
    | GenerateReTweet of int
    | StoreTweet of tweetStorageUser
    | TweetRegistered of tweetStorageUser
    | SearchThis of String
    | Display of Set<tweetStorageUser>
    | Login
    | Logout
    | UpdateHome of Set<tweetStorageUser>
    | RetweetTest
    | DisplayFollowers
    | DisplayFollowing
    | DisplayHomeTimeLine
    | HomeInitialize4
    | AddFollowerNew of int
    | UpdateFollowingNew of int
  
type engineMessage= 
    | Initialize of int
    | RegisterNewClient of int*IActorRef
    | Complete
    | PopulateFromSimulator of Map<int,Set<int>>*Map<int,Set<int>>*Set<int>
    | RegisterTweet of String*int
    | RegisterReTweet of int*int
    | SearchServer of String
    | SwitchServer of int
    | DisplayFollowersS of int
    | DisplayFollowingS of int
    | Register4 of int
    | UpdateFollow of int*int
  
type engineFanoutMessage = 
    | Fanout of tweetStorageUser*Set<IActorRef>
  
type tweetStructure = struct
    val tweetId: int
    val tweet: String
    val creator: int
  
    new (tid,msg,cid) = {tweetId = tid; tweet = msg; creator = cid;}
  
end
  
type retweetStructure = struct
    val retweetId: int
    val tweetId: int
    val sharedBy: int
  
    new (rtid,tid,sid) = {retweetId = rtid;tweetId = tid;sharedBy = sid}
end

let system = System.create "system" (Configuration.defaultConfig())
let mutable numberOfClients = 500 
let mutable socketMap = Map.empty<int,WebSocket>
let mutable globalusermap = Map.empty<int,IActorRef>
let mutable actorrefmap = Map.empty<int,IActorRef>
let mutable userDetailMap = Map.empty<String,userDetails>

let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    let mutable loop = true
    while loop do
      let! msg = webSocket.read()
      match msg with
      | (Ping, data, false) ->
        printfn "true"
      | (Text, data, true) ->
        let str = UTF8.toString data
        let mutable response = sprintf "%s" str
        
        if (response.Contains "SIGNUP") then 
            let result = response.Split '&'
            let id = numberOfClients + 1
            numberOfClients <- numberOfClients + 1
            let fname = (result.[1].Split ':').[1]
            let lname = (result.[2].Split ':').[1]
            let email = (result.[3].Split ':').[1]
            let pwd = (result.[4].Split ':').[1]
            let un = fname + lname
            printfn "%s signep up." un
            userDetailMap <- userDetailMap.Add(un, new userDetails(id,fname,lname,un,email,pwd))
            actorrefmap.[0] <! Register4 id
            response <- "Signup Successful"

        if (response.Contains "LOGIN") then
            let result = response.Split '&'
            let username = (result.[1].Split ':').[1]
            let pwd = (result.[2].Split ':').[1]
            if (userDetailMap.[username].Password = pwd) then
                let myid = userDetailMap.[username].userId
                response <- "Login Succesful"
                printfn "User%i Logged in!" myid
                globalusermap.[myid] <! Login
            else
                response <- "Wrong Credentials"
            
        if (response.Contains "HOME") then 
            let result = response.Split '&'
            let username = (result.[1].Split ':').[1]
            let myid = userDetailMap.[username].userId
            socketMap <- socketMap.Add(myid,webSocket)
            globalusermap.[myid] <! HomeInitialize4 // Client actor

        if (response.Contains "TWEET") then 
            let result = response.Split '&'
            let username = (result.[1].Split ':').[1]
            let tweet = (result.[2].Split ':').[1]
            let myid = userDetailMap.[username].userId
            globalusermap.[myid] <! GenerateTweet tweet

        if (response.Contains "RTWT") then 
            let result = response.Split '&'
            let username = (result.[1].Split ':').[1]
            let tweetid = (result.[2].Split ':').[1] |>int
            let myid = userDetailMap.[username].userId
            globalusermap.[myid] <! GenerateReTweet tweetid

        if (response.Contains "SEARCH") then 
            let result = response.Split '&'
            let username = (result.[1].Split ':').[1]
            let searchtext = (result.[2].Split ':').[1] 
            let myid = userDetailMap.[username].userId
            globalusermap.[myid] <! SearchThis searchtext

        if (response.Contains "FOLLOWXYZ") then 
            let result = response.Split '&'
            let username = (result.[1].Split ':').[1]
            let tobefollowed = (result.[2].Split ':').[1] 
            let myid = userDetailMap.[username].userId
            let id = userDetailMap.[tobefollowed].userId
            globalusermap.[myid] <! AddFollowerNew id

        if (response.Contains "LOGOUT") then
            let result = response.Split '&'
            let username = (result.[1].Split ':').[1]
            printfn "%s logged out!" username
            let myid = userDetailMap.[username].userId
            globalusermap.[myid] <! Logout


        let byteResponse =
          response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment


        // the `send` function sends a message back to the client
        do! webSocket.send Text byteResponse true
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
    GET >=> choose [ path "/" >=> file "index.html"]
    GET >=> choose [ path "/signup" >=> file "signup.html"]
    GET >=> choose [ path "/login" >=> file "login.html"]
    GET >=> choose [ path "/home" >=> file "home.html"]
    NOT_FOUND "Found no handlers." ]





[<EntryPoint>]
let main _ =
    let system = System.create "system" (Configuration.defaultConfig())
    let mutable client = null 
    let tweetList = new List<String>()
  
    for i = 1 to 10 do
        tweetList.Add(sprintf "This is #tweet%i @User%i" i i )
  
    let mutable test1bool = false
    let mutable clientGenerator = null
    let mutable twitterEngine = null
    let mutable simulator = null
    let mutable engineFanOut = null
    let sha = SHA256.Create()
    let mutable rf = 2
    let mutable TotalfollowersCount = 0

    
  
    if numberOfClients < 500 then 
        printfn "The number of clients inputted is too low, taken 500 as default!"
        numberOfClients <- 500
  
    let mutable terminator = 0

    

    let client (mailbox : Actor<_>) =
        let mutable myId = 0 
        let mutable activeState = true
        let mutable followerSet = Set.empty<int>
        let mutable followingSet = Set.empty<int>
        let mutable homeTimeline = new List<tweetStorageUser>()
        let mutable userTimeLine = new List<tweetStorageUser>()
        let random = System.Random()
        let rec loop() = actor {
            let! msg = mailbox.Receive()
            match msg with
            | InitializeClient (id)->
                myId <- id
            | Login ->
                twitterEngine <! SwitchServer myId
                activeState <- true
            |Logout ->
                twitterEngine <! SwitchServer myId
                activeState <- false            
            | PopulateFollowers(set) ->
                followerSet <- set
            | PopulateFollowing (set) ->
                followingSet <- set
            | AddFollower (follower) ->
                followerSet <- followerSet.Add(follower)
            | AddFollowing (following) ->
                followingSet <- followingSet.Add(following)
            | GenerateTweet (tweet)->
                twitterEngine <! RegisterTweet (tweet,myId)
            | GenerateReTweet (tweetId) -> 
                twitterEngine <! RegisterReTweet (tweetId,myId)
            | StoreTweet tweet ->
                if tweet.isRetweet then 
                    simulator <! CountTweetByAll 2
                else 
                    simulator <! CountTweetByAll 1
                //printfn "%i Got Tweet: %s from %i with a id %i and is a reTweet %b with id %i and shared by %i " myId tweet.tweet tweet.creator tweet.tweetId tweet.isRetweet tweet.retweetId tweet.sharedBy
                homeTimeline.Add(tweet)
                if (socketMap.ContainsKey(myId)) then
                    let maketweet (tweet:tweetStorageUser) = 
                        let tid = (sprintf "%i" tweet.tweetId)
                        let rtid = (sprintf "%i" tweet.retweetId)
                        let cid = (sprintf "User%i" tweet.creator)
                        let sid = (sprintf "User%i" tweet.sharedBy)
                        let isr = tweet.isRetweet|>string
                        let mutable str = "type:htxyz&tid:"+tid+"&text:"+tweet.tweet+"&creator:"+cid+"&isrt:"+isr+ "&rtid:"+rtid+ "&sby:"+sid
                        str
                    let str = maketweet(tweet)
                    let byteResponse1 =
                        str
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    
                    let sendmessage (webSocket : WebSocket)=  
                        webSocket.send Text byteResponse1 true
                    Async.RunSynchronously(sendmessage socketMap.[myId])
            | TweetRegistered (tweet) ->
                userTimeLine.Add(tweet)
            | SearchThis item ->
                twitterEngine <! SearchServer (item)
            | Display set ->
                let maketweet (tweet:tweetStorageUser) = 
                    let tid = (sprintf "%i" tweet.tweetId)
                    let rtid = (sprintf "%i" tweet.retweetId)
                    let cid = (sprintf "User%i" tweet.creator)
                    let sid = (sprintf "User%i" tweet.sharedBy)
                    let isr = tweet.isRetweet|>string
                    let mutable str = "type:sxyz&tid:"+tid+"&text:"+tweet.tweet+"&creator:"+cid+"&isrt:"+isr+ "&rtid:"+rtid+ "&sby:"+sid
                    str
                for i in set do 
                    let str = maketweet(i)
                    let byteResponse1 =
                        str
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    
                    let sendmessage (webSocket : WebSocket)=  
                        webSocket.send Text byteResponse1 true
                    Async.RunSynchronously(sendmessage socketMap.[myId])
                    
                        
            | UpdateHome set ->
                for tweet in set do 
                    homeTimeline.Add(tweet)
            | RetweetTest ->
                let mutable tweet = random.Next(0,homeTimeline.Count)
                mailbox.Self <! GenerateReTweet homeTimeline.[tweet].tweetId
                
            | DisplayFollowers ->
                for user in followerSet do 
                    printf "%i " user
                let list = new List<int>(followerSet)
                let randomFollower = list.[random.Next(0,list.Count)]
                printfn "Random follower Selected is %i" randomFollower
                rf <- randomFollower
                terminator <- terminator + 1
            | DisplayFollowing ->
                printfn "Client Following!"
                for user in followingSet do 
                    printf "%i " user
                printfn ""
            | DisplayHomeTimeLine ->
                printfn ""
                for tweet = homeTimeline.Count-1 downto Math.Max(homeTimeline.Count-6,0) do 
                    printfn "Tweet Id : %i" homeTimeline.[tweet].tweetId
                    printfn "Tweet Text : %s" homeTimeline.[tweet].tweet
                    printfn "Tweet Creator: User%i"  homeTimeline.[tweet].creator
                    if homeTimeline.[tweet].isRetweet then 
                        printfn "Retweeted by User%i" homeTimeline.[tweet].sharedBy
                    printfn ""
            | HomeInitialize4 ->
                let maketweet (tweet:tweetStorageUser) = 
                    let tid = (sprintf "%i" tweet.tweetId)
                    let rtid = (sprintf "%i" tweet.retweetId)
                    let cid = (sprintf "User%i" tweet.creator)
                    let sid = (sprintf "User%i" tweet.sharedBy)
                    let isr = tweet.isRetweet|>string
                    let mutable str = "type:hi4xyz&tid:"+tid+"&text:"+tweet.tweet+"&creator:"+cid+"&isrt:"+isr+ "&rtid:"+rtid+ "&sby:"+sid
                    str

                let makeuser (id: int)=
                    let mutable str = "type:dfl&user:" + (id|>string)
                    str
                
                let makeuser1 (id: int)=
                    let mutable str = "type:dofl&user:" + (id|>string)
                    str

                for i = 0 to homeTimeline.Count - 1 do 
                    let str = maketweet(homeTimeline.[i])
                    let byteResponse1 =
                        str
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    
                    let sendmessage (webSocket : WebSocket)=  
                        webSocket.send Text byteResponse1 true
                    Async.RunSynchronously(sendmessage socketMap.[myId])
                
                for i in followerSet do 
                    let str = makeuser i
                    let byteResponse1 =
                        str
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    
                    let sendmessage (webSocket : WebSocket)=  
                        webSocket.send Text byteResponse1 true
                    Async.RunSynchronously(sendmessage socketMap.[myId])

                for i in followingSet do 
                    let str = makeuser1 i
                    let byteResponse1 =
                        str
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    
                    let sendmessage (webSocket : WebSocket)=  
                        webSocket.send Text byteResponse1 true
                    Async.RunSynchronously(sendmessage socketMap.[myId])
            | AddFollowerNew (tobefollowed) ->
                followingSet <- followingSet.Add(tobefollowed)
                twitterEngine <! UpdateFollow (tobefollowed,myId)

                let mutable str = "type:dofl&user:" + (tobefollowed |>string)
                let byteResponse1 =
                    str
                    |> System.Text.Encoding.ASCII.GetBytes
                    |> ByteSegment
                    
                let sendmessage (webSocket : WebSocket)=  
                    webSocket.send Text byteResponse1 true
                Async.RunSynchronously(sendmessage socketMap.[myId])
            
            | UpdateFollowingNew (follower) ->
                followerSet <- followerSet.Add(follower)
                let mutable str = "type:dfl&user:" + (follower|>string)
                let byteResponse1 =
                    str
                    |> System.Text.Encoding.ASCII.GetBytes
                    |> ByteSegment
                    
                let sendmessage (webSocket : WebSocket)=  
                    webSocket.send Text byteResponse1 true
                Async.RunSynchronously(sendmessage socketMap.[myId])

          
            return! loop()
        }
        loop()

    let simulatorActor  (mailbox : Actor<_>) = 
        let mutable simulatorIsActive = new List<bool>()
        let mutable simulatorFollowerMap = Map.empty<int,Set<int>>
        let mutable simulatorFollowingMap = Map.empty<int,Set<int>>
        let mutable simulatorUserMap = Map.empty<int,IActorRef> 
        let mutable simulatorCelebSet = Set.empty<int>
        let random = System.Random()
        let mutable numberOfClients = 0
        let mutable zipfConstant = 0.0
        let mutable countTweetByAll = 0
        let rec loop() = actor {
            let! msg = mailbox.Receive()
            match msg with
            | Start (num)->
                numberOfClients <- num
                let zipf = 
                    let mutable c = 0.0
                    for i = 1 to numberOfClients do
                        let j = i|> float
                        c <- c + (1.0/j)
                    (1.0/c)
                zipfConstant <- zipf
                simulatorIsActive.Add(true)
                for i = 1 to numberOfClients do 
                    let mutable set = Set.empty
                    simulatorFollowerMap <- simulatorFollowerMap.Add(i,set)
                    simulatorFollowingMap <- simulatorFollowingMap.Add(i,set)
                    simulatorIsActive.Add(true)
                twitterEngine <! Initialize numberOfClients
                clientGenerator <! SpawnClient numberOfClients
            | GetAllUser (map) ->
                simulatorUserMap <- map
                simulator <! SimulateConnections
            | SimulateConnections ->
                let celebNumber = (5*numberOfClients)/100
                while (simulatorCelebSet.Count < celebNumber) do 
                    simulatorCelebSet <- simulatorCelebSet.Add(random.Next(1,numberOfClients+1))
                //printfn "Celebs: %i" simulatorCelebSet.Count
                    
                    
                let populateFollowers (userId,followerCount) =    
                    while (simulatorFollowerMap.[userId].Count <= followerCount) do 
                        let follower = random.Next(1,numberOfClients+1)
                        if(follower <> userId && not (simulatorCelebSet.Contains(follower))) then 
                            simulatorFollowerMap <- simulatorFollowerMap.Add(userId,simulatorFollowerMap.[userId].Add(follower))
                            simulatorFollowingMap <- simulatorFollowingMap.Add(follower,simulatorFollowingMap.[follower].Add(userId))
                    simulatorUserMap.[userId] <! PopulateFollowers simulatorFollowerMap.[userId]
                    //printfn "User%i has %i followers." userId simulatorFollowerMap.[userId].Count
                        
        
                let populateCelebFollowing = 
                    let mutable celebList = new List<int>(simulatorCelebSet)
                    for celeb in simulatorCelebSet do 
                        let celebfollowing = random.Next((simulatorCelebSet.Count*10)/100,(simulatorCelebSet.Count*20)/100)
                        while(simulatorFollowingMap.[celeb].Count < celebfollowing) do 
                            let mutable celebToFollow = celebList.[random.Next(0,simulatorCelebSet.Count)]
                            if(celebToFollow <> celeb) then 
                                simulatorFollowingMap <- simulatorFollowingMap.Add(celeb,simulatorFollowingMap.[celeb].Add(celebToFollow))
                                simulatorUserMap.[celebToFollow] <! AddFollower celeb
                                simulatorFollowerMap <- simulatorFollowerMap.Add(celebToFollow,simulatorFollowerMap.[celebToFollow].Add(celeb))    
                    
                let celebFollowerCount = (zipfConstant*(numberOfClients|>float)) |>int
                let celebLowerLimit = Math.Max(celebFollowerCount - (celebFollowerCount*10)/100,0)
                let celebUpperLimit = celebFollowerCount + (celebFollowerCount*10)/100
                for celeb in simulatorCelebSet do 
                    populateFollowers (celeb,random.Next(celebLowerLimit,celebUpperLimit))
                                
                for userId = 1 to numberOfClients do 
                    if not (simulatorCelebSet.Contains(userId)) then
                        let followerCount =  ((zipfConstant*(numberOfClients|>float))|> int)/random.Next(2,12) + 1
                        let lowerLimit = Math.Max(followerCount - (followerCount*10)/100,0)
                        let upperLimit = followerCount + (followerCount*10)/100
                        populateFollowers (userId,random.Next(lowerLimit,upperLimit))
        
                populateCelebFollowing
        
                for user in simulatorUserMap do 
                    user.Value <! PopulateFollowing simulatorFollowingMap.[user.Key]
                    //printfn "User%i follows %i people and is followed by %i people" user.Key simulatorFollowingMap.[user.Key].Count simulatorFollowerMap.[user.Key].Count   
        
                for user in simulatorFollowerMap do 
                    TotalfollowersCount <- TotalfollowersCount + user.Value.Count
        
                twitterEngine <! PopulateFromSimulator (simulatorFollowerMap,simulatorFollowingMap,simulatorCelebSet)
                Threading.Thread.Sleep(100)
                terminator <- terminator + 1 
        
            | SimulateTweet (tweet,userId) ->
                //printfn "User%i tweeted %s" userId tweet
                simulatorUserMap.[userId] <! GenerateTweet tweet
            | SimulateReTweet (tweetId,userId)->
                simulatorUserMap.[userId] <! GenerateReTweet tweetId
            | SearchFromClient (item,userId)->
                simulatorUserMap.[userId] <! SearchThis item
            | TweetByAll (tweet) ->
                for i = 1 to numberOfClients do 
                    simulatorUserMap.[i] <! GenerateTweet tweet
            | CountTweetByAll (testId)->
                countTweetByAll <- countTweetByAll + 1
                //printfn "%i" countTweetByAll
                if testId = 1 then 
                    if countTweetByAll >= 2* TotalfollowersCount-1 then 
                        test1bool <- true
                else if testId = 2 then 
                    if countTweetByAll >= TotalfollowersCount then 
                        test1bool <- true
            | Reset -> 
                countTweetByAll <- 0
            | ReTweetByAll ->
                for i = 1 to numberOfClients do 
                    simulatorUserMap.[i] <! RetweetTest
            | DisplayFollowerClient userId ->
                simulatorUserMap.[userId] <! DisplayFollowers
            | DisplayFollowingClient userId ->
                simulatorUserMap.[userId] <! DisplayFollowing
            | DisplayFollowerServer userId ->
                twitterEngine <! DisplayFollowersS userId
            | DisplayFollowingServer userId ->
                twitterEngine <! DisplayFollowingS userId
            | SwitchSimulator user ->
                if simulatorIsActive.[user] then 
                    simulatorUserMap.[user] <! Logout
                else 
                    simulatorUserMap.[user] <! Login
                simulatorIsActive.[user] <- not simulatorIsActive.[user] 
            | DisplayHT user ->
                simulatorUserMap.[user] <! DisplayHomeTimeLine
        
                    
        
            return! loop()
        }
        loop()
        
    let clientgenerator  (mailbox : Actor<_>) = 
        let rec loop() = actor {
            let! msg = mailbox.Receive()
            match msg with
            | SpawnClient (numOfClients) ->
                for userId = 1 to numOfClients do 
                    let client = spawn system (sprintf "User%i" userId) client
                    let name = sprintf "User%i" userId
                    let email = sprintf "User%i@twitter.com" userId
                    userDetailMap <- userDetailMap.Add(name,new userDetails(userId,name,"",name,email,name))
                    client<! InitializeClient userId
                    globalusermap <- globalusermap.Add(userId,client)
                    twitterEngine <! RegisterNewClient (userId,client)
        
                twitterEngine <! Complete
            return! loop()
        }
        loop()
        
    let engine (mailbox : Actor<_>) =
        let mutable activeList = new List<bool>()
        let mutable followerMap = Map.empty<int,Set<int>>
        let mutable followingMap = Map.empty<int,Set<int>>
        let mutable userMap = Map.empty<int,IActorRef> 
        let mutable celebSet = Set.empty<int>
        let mutable tweetIdCounter = 1 
        let mutable reTweetIdCounter = -1
        let mutable tweetMap = Map.empty<int,tweetStructure>
        let mutable reTweetMap = Map.empty<int,retweetStructure>
        let mutable hashtagMap = Map.empty<String,Set<int>>
        let random = System.Random()
        let mutable totalClients = 0 
        let mutable wordMap = Map.empty<String,Set<int>> 
        let mutable mentionMap = Map.empty<String,Set<int>> 
        let mutable pendingTweetMap = Map.empty<int,Set<tweetStorageUser>>
        let findTags (tweet:String) tweetIdCounter = 
                    let populateMaps (list:List<String>) x =
                        let mutable map = Map.empty<String,Set<int>>
                        if x = 1 then 
                            map <- hashtagMap
                        else if x = 2 then 
                            map <- wordMap
                        else 
                            map <- mentionMap 
        
                        for word in list do
                            if (map.ContainsKey(word)) then 
                                map <- map.Add(word,map.[word].Add(tweetIdCounter))
                            else
                                let set = Set.empty<int>.Add(tweetIdCounter)
                                map <- map.Add(word,set)
                        if x = 1 then 
                            hashtagMap <- map
                        else if x = 2 then 
                            wordMap <- map
                        else 
                            mentionMap <- map
        
                    let hashtagList = new List<String>()
                    let mentionList = new List<String>()
                    let wordList = new List<String>()
                    let tweetBreak = tweet.Split ' '
                    
                    for word in tweetBreak do
                        if (word <> "") then
                            if(word.[0] = '#') then 
                                hashtagList.Add(word)
                            else if(word.[0] = '@') then 
                                mentionList.Add(word)
                            else
                                wordList.Add(word)
                    populateMaps hashtagList 1
                    populateMaps mentionList 3
                    populateMaps wordList 2
        
        let createFollowerRefMap creator tweetStructure=
            let mutable set = followerMap.[creator]
            let mutable res = Set.empty<IActorRef>
        
            for aFollower in set do 
                if activeList.[aFollower] = true then 
                    res <- res.Add(userMap.[aFollower])
                else
                    pendingTweetMap <- pendingTweetMap.Add(aFollower,pendingTweetMap.[aFollower].Add(tweetStructure))
            res
        
        let rec loop() = actor {
            let! msg = mailbox.Receive()
            match msg with
            |Initialize (numOfClients) ->
                totalClients <- numOfClients
                activeList.Add(true)
                for i = 1 to totalClients do 
                    let mutable set = Set.empty
                    let mutable set1 = Set.empty
                    followerMap <- followerMap.Add(i,set)
                    followingMap <- followingMap.Add(i,set)
                    pendingTweetMap <- pendingTweetMap.Add(i,set1)
                    activeList.Add(true)
            
            |Register4 (uid) ->
                let client = spawn system (sprintf "User%i" uid) client
                client<! InitializeClient uid
                userMap <- userMap.Add(uid,client)
                let mutable set = Set.empty
                let mutable set1 = Set.empty
                followerMap <- followerMap.Add(uid,set)
                followingMap <- followingMap.Add(uid,set)
                pendingTweetMap <- pendingTweetMap.Add(uid,set1)
                globalusermap <- globalusermap.Add(uid,client)
                activeList.Add(false)
        
            |RegisterNewClient (clientId,actorReference)->
                userMap <- userMap.Add(clientId,actorReference)
        
            |Complete ->
                simulator <! GetAllUser userMap
                
            |PopulateFromSimulator (follower,following,celeb) -> 
                followerMap <- follower
                followingMap <- following
                celebSet <- celeb
                //printfn "%i %i %i" followerMap.[1].Count followingMap.[100].Count celeb.Count
            |RegisterTweet (tweet,creator) ->
                //printfn "Got tweet %i : %s and sending it to %i followers" creator tweet followerMap.[creator].Count
                tweetMap <- tweetMap.Add(tweetIdCounter,new tweetStructure(tweetIdCounter,tweet,creator))
                findTags tweet tweetIdCounter
                let tweetStruct = new tweetStorageUser(tweetIdCounter,tweet,creator,false,0,0)
                userMap.[creator] <! TweetRegistered tweetStruct
                //printfn "Count: %A" hashtagMap
                let fanoutSet = createFollowerRefMap creator tweetStruct
                
                engineFanOut <! Fanout (tweetStruct, fanoutSet)
                tweetIdCounter <- tweetIdCounter + 1    
        
            | RegisterReTweet (tweetId,sharedBy) ->
                reTweetMap <- reTweetMap.Add(reTweetIdCounter,new retweetStructure(reTweetIdCounter,tweetId,sharedBy))
                let tweet = tweetMap.[tweetId].tweet
                let creator = tweetMap.[tweetId].creator
                findTags tweet reTweetIdCounter
                let tweetStruct = new tweetStorageUser(tweetId,tweet,creator,true,reTweetIdCounter,sharedBy)
                userMap.[sharedBy] <! TweetRegistered tweetStruct
                engineFanOut <! Fanout (tweetStruct,(createFollowerRefMap sharedBy tweetStruct))
                reTweetIdCounter <- reTweetIdCounter - 1
        
            | SearchServer item ->
                let createDisplay (set:Set<int>) = 
                    let mutable tweetSet = Set.empty<tweetStorageUser>
                    for tweetId in set do
                        if tweetId > 0 then
                            tweetSet <- tweetSet.Add(new tweetStorageUser(tweetId,tweetMap.[tweetId].tweet,tweetMap.[tweetId].creator,false,0,0))
                    
                    tweetSet
        
                let mutable displaySet = Set.empty<tweetStorageUser>
                if item.[0] = '#' && hashtagMap.ContainsKey(item) then
                    displaySet <- createDisplay hashtagMap.[item] 
                else if item.[0] = '@' && mentionMap.ContainsKey(item) then
                    displaySet <- createDisplay mentionMap.[item]  
                else if wordMap.ContainsKey(item) then
                    displaySet <- createDisplay wordMap.[item] 
        
                mailbox.Sender() <! Display displaySet
        
            | SwitchServer userId ->
                activeList.[userId] <-(not activeList.[userId])
                if activeList.[userId] = true then 
                    userMap.[userId] <! UpdateHome pendingTweetMap.[userId]
                    pendingTweetMap <- pendingTweetMap.Add(userId,Set.empty)
        
            | DisplayFollowersS id->
                printfn "Follower List."
                for user in followerMap.[id] do 
                    printf "%i " user
                printfn ""
        
            | DisplayFollowingS id->
                printfn "Following List."
                for user in followingMap.[id] do 
                    printf "%i " user
                printfn ""
            | UpdateFollow (tobefollowed,follower)->
                followingMap <- followingMap.Add(follower,followingMap.[follower].Add(tobefollowed))
                followerMap <- followerMap.Add(tobefollowed,followerMap.[tobefollowed].Add(follower))
                userMap.[tobefollowed] <! UpdateFollowingNew follower
            return! loop()
        }
        loop()
        
        
    let engineFanout (mailbox : Actor<_>) =
        let rec loop() = actor {
            let! msg = mailbox.Receive()
            match msg with
            | Fanout (tweetStorageUser,followerSet) ->
                for user in followerSet do 
                    user <! StoreTweet tweetStorageUser
                    
            return! loop()
        }
        loop()
        
    twitterEngine <- spawn system "twitterEngine" engine
    engineFanOut <- spawn system "Fanout" engineFanout
    simulator <- spawn system "simulator" simulatorActor
    clientGenerator <- spawn system "clientGenerator" clientgenerator

    actorrefmap <- actorrefmap.Add(0,twitterEngine)
    actorrefmap <- actorrefmap.Add(1,engineFanOut)
    actorrefmap <- actorrefmap.Add(2,simulator)
    actorrefmap <- actorrefmap.Add(3,clientGenerator)
    printfn "Twitter Clone "
    simulator <! Start (numberOfClients)
        
    let mutable check = true
        
    while terminator = 0 do
        check <- false
        
    printfn "Twitter Clone for %d Users have been built succesfully!" numberOfClients
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    for i = 0 to 1 do 
        simulator <! TweetByAll tweetList.[i]
        
    while not test1bool do 
        check <- check
        
    stopwatch.Stop()
    printfn "Time taken(in ms) to transmit and receive %i tweets in the system is: %f" (TotalfollowersCount*2) stopwatch.Elapsed.TotalMilliseconds
        
    printfn "- - - -- - - - - - -- - - - - - -- - - - - - -- - - - - - - -- - - - -- - - - -- - - - -- - - - -- - - "
        
    simulator <! Reset
    test1bool <- false
        
    stopwatch.Reset()
    stopwatch.Start()
    simulator <! ReTweetByAll 
        
    while not test1bool do 
        check <- check
    stopwatch.Stop()
    printfn "Time taken(in ms) to transmit and receive %i retweets in the system is: %f" (TotalfollowersCount) stopwatch.Elapsed.TotalMilliseconds
     
    for i = 1 to numberOfClients do 
        globalusermap.[i] <! Logout
        
    
  
    startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app

    0