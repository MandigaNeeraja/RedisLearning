# Foundation of Redis

Redis is an in-memory data store. You talk to it with simple commands, and it keeps data in RAM so reads and writes are very fast.

People use Redis when a normal database is too slow for a small, hot piece of data: a cache, a login session, a counter, a queue, or a lock.

This guide covers the ideas you need before you write application code.

## What Redis is

Redis stores **values under keys**, like a giant dictionary that lives on a server.

```text
user:42:name   ->  "Asha"
page:home:views ->  18420
session:abc     ->  { "userId": 42, "role": "admin" }
```

A few facts that shape everything else:

- **Data lives in memory.** That is why Redis is fast. RAM is much quicker than a disk.
- **It is still a database.** You can ask Redis to save data to disk so it survives a restart.
- **Commands are atomic.** One command finishes completely before the next one starts. Two clients cannot half-update the same key at the same time.
- **Keys expire.** You can say "delete this key after 10 minutes." That is how caches and sessions clean themselves up.
- **It is not a replacement for your main database.** Redis is best for data you need quickly, or for short-lived data. Your source of truth (users, orders, posts) usually stays in Postgres, MySQL, or similar.

The server is called **Redis**. The command-line client is called **redis-cli**. Your app talks to Redis over the network, usually on port `6379`.

## A key, a value, and a command

Every Redis command has the same shape:

```text
COMMAND key [arguments]
```

Try this in `redis-cli`:

```redis
SET greeting "hello"
GET greeting
```

`SET` stores a string. `GET` reads it back. Redis replies:

```text
OK
"hello"
```

Keys are just strings you invent. A good habit is to use a prefix and a colon so keys stay readable:

```text
user:42:email
cart:42
rate:login:203.0.113.5
```

Redis does not create folders from those colons. The colon is only a naming convention so humans (and your code) can tell keys apart.

## Why it feels instant

Three things make Redis fast:

1. **Memory.** The working copy of the data is in RAM.
2. **Simple operations.** `GET`, `SET`, `INCR`, and `HGET` do one small job. They do not scan a whole table.
3. **One command at a time.** Redis runs commands on a single thread. There is no lock around every key for normal commands, because only one command runs at once.

"Single-threaded" does not mean Redis is slow. It means each command is so short that thousands of them can finish in a millisecond. The danger is the opposite: one **slow** command (like scanning every key in a huge database) blocks every other client until it finishes. Prefer commands that touch one key, or a small number of keys.

## The core data types

Redis is a key-value store, but the value is not always a plain string. The type is chosen by the first command you use on that key. You cannot store a string in a key and then treat that same key as a list.

| Type | What it holds | Think of it as | Everyday use |
| --- | --- | --- | --- |
| String | Text, a number, or a small blob | One value | Cache, counter, flag |
| Hash | Field and value pairs | One row, or a small object | User profile, session |
| List | Ordered values | A queue or a feed | Background jobs, recent items |
| Set | Unique values, no order | A bag of unique IDs | Tags, "who liked this" |
| Sorted set | Unique values with a score | A leaderboard | Rankings, rate limits |
| Stream | An append-only log of events | A durable queue | Event processing |

There is also **Pub/Sub** (live messages, not stored) and a few newer types (JSON, vector search) in Redis Stack. Learn the six above first.

### String

The simplest type. The value can be text or a number Redis can increment.

```redis
SET user:42:name "Asha"
GET user:42:name

SET page:home:views 0
INCR page:home:views
INCRBY page:home:views 10
GET page:home:views
```

`INCR` adds 1. It creates the key at `0` and then increments if the key does not exist. That is safe under many clients because the increment is one atomic command. You do not read, add 1 in your app, and write back.

Strings also hold cached JSON. Redis does not parse it. Your app stores a string and parses it later:

```redis
SET cache:product:99 "{\"name\":\"Notebook\",\"price\":12.5}"
GET cache:product:99
```

Useful string commands: `SET`, `GET`, `MGET`, `INCR`, `INCRBY`, `APPEND`, `GETRANGE`, `DEL`.

### Hash

A hash is a small object stored under one key. Each field is its own value, so you can read or update one field without rewriting the whole object.

```redis
HSET user:42 name "Asha" email "asha@example.com" plan "free"
HGET user:42 email
HGETALL user:42
HINCRBY user:42 loginCount 1
```

Use a hash when you have several related fields and you update them independently. Use a string when you always read and write the whole value together (a cached JSON document is a common case).

Useful hash commands: `HSET`, `HGET`, `HMGET`, `HGETALL`, `HDEL`, `HINCRBY`, `HEXISTS`.

### List

A list keeps values in order. You push on one end and pop from the other.

```redis
LPUSH recent:42 "item-3"
LPUSH recent:42 "item-2"
LPUSH recent:42 "item-1"
LRANGE recent:42 0 9
```

`LPUSH` adds to the head (the left side). `LRANGE` reads a slice. `0 9` means "the first 10 items." Lists are 0-indexed.

A queue uses both ends:

```redis
LPUSH jobs "send-email:42"
RPOP jobs
```

Workers often use `BLPOP` or `BRPOP`. The `B` means **blocking**: the command waits until a job appears, instead of your code looping and asking "anything yet?"

```redis
BRPOP jobs 5
```

That waits up to 5 seconds. If a job arrives, Redis returns it. If not, it returns nil.

Useful list commands: `LPUSH`, `RPUSH`, `LPOP`, `RPOP`, `LRANGE`, `LLEN`, `BLPOP`, `BRPOP`.

### Set

A set stores unique values. Adding the same member twice does nothing. Order is not kept.

```redis
SADD post:7:likes 42
SADD post:7:likes 17
SADD post:7:likes 42
SCARD post:7:likes
SISMEMBER post:7:likes 17
SMEMBERS post:7:likes
```

`SCARD` is the count. `SISMEMBER` answers yes or no. That "is this user in the set?" check is O(1): it stays fast as the set grows.

Sets can be combined:

```redis
SINTER user:1:friends user:2:friends
```

`SINTER` returns members that are in both sets. Handy for mutual friends or shared tags.

Useful set commands: `SADD`, `SREM`, `SISMEMBER`, `SMEMBERS`, `SCARD`, `SINTER`, `SUNION`, `SDIFF`.

### Sorted set

A sorted set is a set where each member has a numeric **score**. Redis keeps members ordered by that score.

```redis
ZADD leaderboard 1500 "Asha"
ZADD leaderboard 2200 "Ben"
ZADD leaderboard 1800 "Cia"
ZRANGE leaderboard 0 -1 WITHSCORES
ZREVRANGE leaderboard 0 2 WITHSCORES
ZRANK leaderboard "Asha"
```

`ZRANGE` goes low score to high score. `ZREVRANGE` goes high to low, which is what a leaderboard usually wants. `0 -1` means "from the first item to the last."

Scores do not have to be points. A common trick is to use a timestamp as the score so the set stays ordered by time.

Useful sorted-set commands: `ZADD`, `ZINCRBY`, `ZRANGE`, `ZREVRANGE`, `ZRANK`, `ZSCORE`, `ZREM`, `ZCOUNT`.

### Stream

A stream is a log. Each entry gets an id like `1710000000000-0`, and the entry holds field-value pairs. Consumers can read new entries without deleting them, which makes streams a better job queue than a list when you need history, groups of workers, and acknowledgements.

```redis
XADD orders * userId 42 sku "notebook" qty 1
XRANGE orders - +
```

`*` tells Redis to generate the id from the current time.

You do not need streams on day one. Learn strings, hashes, lists, sets, and sorted sets first. Come back to streams when a list-based queue is not enough (you lost a job because the worker crashed after `RPOP`, for example).

## Keys do not live forever

`EXPIRE` sets a timeout in seconds. When the time is up, Redis deletes the key.

```redis
SET session:abc "user-42"
EXPIRE session:abc 600
TTL session:abc
```

`TTL` returns the remaining seconds. `-1` means the key exists and will not expire. `-2` means the key is already gone.

You can set the value and the lifetime in one command:

```redis
SET cache:product:99 "{\"name\":\"Notebook\"}" EX 60
```

`EX 60` means "expire after 60 seconds."

This is the heart of caching and sessions. You do not write a cleanup job. Redis deletes the key for you.

`PERSIST` removes the timeout. `DEL` deletes the key now.

## A command finishes, then the next one starts

Because one command runs at a time, this counter is safe even if 1,000 clients call it together:

```redis
INCR page:home:views
```

This pattern is **not** safe if you split it across two commands in your application:

```text
count = GET page:home:views
SET page:home:views (count + 1)
```

Two clients can read the same number, both add 1, and one update is lost. When you need a read-then-write to be atomic, either use a single command (`INCR`, `HINCRBY`, `SET` with options) or a small Lua script via `EVAL`. You will not need Lua at the start.

`SET` can also store a value only when the key is absent. That is a simple lock:

```redis
SET lock:invoice:99 "worker-1" NX EX 30
```

- `NX` means "set only if the key does Not eXist."
- `EX 30` means the lock expires after 30 seconds, so a crashed worker does not hold it forever.

If Redis replies `OK`, you got the lock. If it replies nil, someone else holds it.

## What people build with it

### Cache

The app checks Redis first. If the value is there, return it. If not, read the database, store the result in Redis with a timeout, and return it.

```text
1. GET cache:product:99
2. If missing, SELECT from the database
3. SET cache:product:99 "<json>" EX 60
4. Return the product
```

When the product changes, delete the key (`DEL cache:product:99`) so the next read fills the cache again. This is called **cache invalidation**. Getting it wrong is the usual caching bug: the cache still shows old data.

### Session

After login, create a random id, store the user id in Redis, and send the random id to the browser as a cookie.

```redis
SET session:8f3a... "42" EX 86400
```

The cookie holds the session id, not the user id. Redis holds the link, and it disappears after a day (`86400` seconds).

### Counter and rate limit

```redis
INCR rate:login:203.0.113.5
EXPIRE rate:login:203.0.113.5 60
```

Set the expiry only when the key is new, otherwise every failed login refreshes the window. A clean way:

```redis
SET rate:login:203.0.113.5 0 NX EX 60
INCR rate:login:203.0.113.5
```

If the count passes your limit, reject the request. The key disappears after 60 seconds and the window starts over.

### Leaderboard

```redis
ZADD leaderboard 10 "Asha"
ZINCRBY leaderboard 5 "Asha"
ZREVRANGE leaderboard 0 9 WITHSCORES
```

### Short queue

```redis
LPUSH jobs "resize-image:55"
BRPOP jobs 0
```

`BRPOP` with timeout `0` waits until a job exists. For anything that must not be lost if a worker crashes, look at **streams** and consumer groups later.

### Pub/Sub

Publish/subscribe sends a message to whoever is listening **right now**. Redis does not store the message. If nobody is subscribed, the message is gone.

```redis
SUBSCRIBE news
PUBLISH news "deploy finished"
```

Run `SUBSCRIBE` in one terminal and `PUBLISH` in another. Good for live notifications. Bad for a job queue, because there is no backlog.

## Persistence: what survives a restart

By default Redis cares most about speed. You choose how much data you can afford to lose.

| Mode | What it does | Tradeoff |
| --- | --- | --- |
| RDB (snapshot) | Writes a compact dump on a schedule, for example every 5 minutes if data changed | Fast restarts. You can lose the writes since the last snapshot. |
| AOF (append-only file) | Logs every write and replays the log on startup | Loses less data. Slightly more disk work. |
| Both | Snapshot plus the log | Common production setup. |

For local learning, the default is fine. Do not put data you cannot recreate only in Redis until you understand this. A cache can be empty after a restart. A shopping cart or a job queue might need AOF.

## Redis in an application

`redis-cli` is how you learn. Your real app uses a client library.

The flow is always:

1. Open a connection (or a pool of connections) to `host:6379`.
2. Send a command.
3. Read the reply.

A Node.js sketch:

```js
import { createClient } from "redis";

const client = createClient({ url: "redis://localhost:6379" });
await client.connect();

await client.set("greeting", "hello");
const value = await client.get("greeting");
console.log(value); // hello

await client.quit();
```

Other languages look the same: `SET`, `GET`, `HSET`, `EXPIRE`. The commands do not change. Only the method names in the library change (`client.set`, `redis.set`, and so on).

If Redis is down, decide what the app should do. A cache miss can fall back to the database. A rate limiter or a lock usually should fail the request instead of pretending Redis answered.

## Commands worth memorizing

| Goal | Command |
| --- | --- |
| Store and read a string | `SET`, `GET` |
| Store with a timeout | `SET key value EX seconds` |
| Delete | `DEL` |
| Does it exist? | `EXISTS` |
| Time left | `TTL` |
| Add 1 | `INCR` |
| Object with fields | `HSET`, `HGET`, `HGETALL` |
| Queue | `LPUSH`, `BRPOP` |
| Unique members | `SADD`, `SISMEMBER` |
| Ranking | `ZADD`, `ZREVRANGE` |
| Find keys by pattern | `SCAN` (see the warning below) |
| What type is this key? | `TYPE` |
| How many keys? | `DBSIZE` |

### Do not use `KEYS` in real data

`KEYS user:*` scans **every** key and blocks the server while it runs. Fine on your laptop with 20 keys. Dangerous on a server with millions.

Use `SCAN` instead. It walks the keyspace in small steps:

```redis
SCAN 0 MATCH user:* COUNT 100
```

The first argument is a cursor. Start at `0`. Each reply gives you a new cursor and a batch of keys. Call `SCAN` again with that cursor until Redis returns cursor `0`.

## Try this on your machine

1. Install Redis and start the server.
   - **Windows:** [Memurai](https://www.memurai.com/) (Redis-compatible), Docker (`docker run --name redis -p 6379:6379 redis`), or WSL with `sudo apt install redis-server`.
   - **macOS:** `brew install redis` then `brew services start redis`.
   - **Linux:** install `redis-server` from your package manager and start it.
2. Open a terminal and run `redis-cli`. You should see a prompt like `127.0.0.1:6379>`.
3. Check the server:

```redis
PING
```

The reply is `PONG`.

4. Walk through a tiny session:

```redis
SET user:1:name "Asha"
HSET user:1 email "asha@example.com" city "Pune"
EXPIRE user:1 300
HGETALL user:1
INCR stats:signups
SADD online 1
SISMEMBER online 1
ZADD scores 10 "Asha"
ZADD scores 25 "Ben"
ZREVRANGE scores 0 -1 WITHSCORES
```

5. Quit with `QUIT` or Ctrl+C.

`FLUSHDB` deletes **every key in the current database**. Use it only on your local practice instance.

## A simple mental model

```text
Your app  --command-->  Redis  --value-->  Your app
                          |
                    RAM (the data)
                          |
                    Disk (optional snapshot or log)
```

- Pick a **key name** that says what the value is.
- Pick a **type** that matches how you read it (whole value, one field, a queue, a unique set, a ranking).
- Set a **TTL** if the data is allowed to disappear.
- Keep commands **small** so Redis stays fast for everyone else.
- Keep the **source of truth** in your main database unless you have a reason not to.

## What to learn next

After these commands feel natural:

1. Build a cache in your language of choice: read Redis, fall back to a fake database, set a 30-second TTL.
2. Add a counter with `INCR` and a rate limit with `SET NX EX` plus `INCR`.
3. Read about **pipelines** (send many commands in one network round trip) and **transactions** (`MULTI` / `EXEC`).
4. Then look at **streams**, **Redis as a lock**, and **replication** (a primary plus copies for failover).

You do not need all of Redis to be productive. Strings, hashes, expiry, and one collection type (list, set, or sorted set) cover most early projects.
