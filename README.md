# Projekt 2: Server pro IPK24-CHAT protokol 
Autor: Dominik Huml <xhumld00@vutbr.cz>

## Teorie
Aplikace je serverem pro IPK24-CHAT protokol. Protokol má dvě varianty, první jako transportní protokol používá TCP<sup>[1]</sup> a druhá varianta používá UDP<sup>[2]</sup>
Pro navázání spojení je využit síťový socket<sup>[3]</sup>. Server podporuje obě varianty naráz.

### Socket
Je to koncový bod který slouží pro posílání a příjímání dat v síti. V kontextu této aplikace bude myšlen síťový socket který se používa pro komuninaci
v TCP/IP sadě<sup>[4]</sup>. Ten je idetifikován číslem portu a IP adresou.

### TCP
TCP je protokol transportní vrstvy. Protkol je spojově orientovaný a zajišťuje spolehlivý přenos dat. Stará se tedy o věci jako duplikace paketů, ztráta paketů, pořadí paketů atd.
Aby tohle všechno mohl zajišťovat, tak musí být navázáno spojení mezi klientem a serverem. Toto spojení je navázáno pomocí tzv. three-way handshake. Protokol nepřenáší "zprávy" ale proud bytů.
Kvůli tomu je potřeba si zvolit nějaký delimitér, který bude určovat kde končí jedna zpráva a kde začíná další. V tomto případě je to `\r\n`.
Obecně je TCP pomalejší než UDP, ale je spolehlivější, což je v případě chatu důležité.

### UDP
UDP je protokol transportní vrstvy. Na rozdíl od TCP je to protokol bez spojení. To znamená, že nepotřebuje navázat spojení mezi klientem a serverem.
Výkon je teda vyšší než u TCP, ale zase nezaručuje doručení zprávy. Tohle je v projektu řešeno až na aplikační vrstvě. 
Aplikace si tedy sama zajišťuje doručení zprávy, takže i použití UDP může být spolehlivé.

## Implementace
Aplikace je napsána v jazyce C#. Zvolil jsem event-driven model, kde primárně využívám události na změnu stavu aplikace.
Dále využívám `Task`<sup>[5]</sup> pro asynchronní zpracování vstupu a výstupu. Je to C# abstrakce nad vlákny, která je mnohem jednodušší na použití.
Je také efektivnější, protože nevytváří nové vlákno pro každý požadavek, ale využívá thread pool. Každý klient je obsluhován ve vlastním tasku.

Aplikaci jsem rozdělil do více tříd, které se starají o různé části aplikace. Díky tomu je možné využít principu kompozice a injekce závislostí.
Hlavní části jsou `ChatClient`, `IProtocol`a `ITransport`. Pro přenos dat jsou pak specifikovány modely, které pak mohou být dále specializovány pro protokol.

![Flow diagram](/ipk-project-2/IPK.Project2.App/Resources/chart.png "Chart")

### Datové modely
Pro obecnou komunikaci je vyvořené rozhraní `IBaseModel`, které si pak implementují třídy dle typu zprávy (např. `AuthModel` pro AUTH zprávu). Tyto zprávy se pak předávají ve všech obecných rozhraních. Protokol UDP pak potřebuje nějaké data navíc (MessageID apod.), kvůli tomu vznikly modely pro UDP. Ty jsou sjednoceny přes rozhraní `IBaseUdpModel`. Tohle rozhraní dále implementuje funkce na binární serializaci a deserializaci, které pomocí reflexe zvládnou zpracovat libovolnou UDP třídu. Validace modelů probíhá přes annotační atributy.

```csharp
    [RegularExpression("[!-~]{1,20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 20 characters")]
    public required string DisplayName { get; set; }
```

### ITransport
Rozhraní které se stará o přenos dat. Implementace tohoto rozhraní je pak závislá na použitém transportním protokolu. 

```csharp
public interface ITransport
{
    public event EventHandler<IBaseModel> OnMessageReceived;
    public event EventHandler OnMessageDelivered;
    public event EventHandler OnConnected;

    public Task StartPrivateConnection();
    public Task Auth(AuthModel data);
    public Task Join(JoinModel data);
    public Task Message(MessageModel data);
    public Task Error(ErrorModel data);
    public Task Bye();
    public Task Start(ProtocolStateBox protocolState);
    public void Disconnect();
}
```
Metoda `Start` je zde jako entrypoint pro začátek komunikace. V této metodě se naváže spojení a začne se poslouchat na nové zprávy.
Ty jsou poté zpracovány a předány dál pomocí eventů. `OnMessageReceived` zašle zprávu na zpracování a `OnMessageDelivered` oznamuje, že zpráva byla úspěšně doručena. V případě TCP je tento event vyvolán okamžitě,
ale UDP ho vyvolá až po úspěšném doručení zprávy, které se děje ve třídě `UdpTransport`.

### IProtocol
Rozhraní které se stará o zpracování vstupních a výstupních dat. Na základě toho pak přepína interní stav aplikace. 
Tyto tranzice se snaží kopírovat konečný stavový automat ze zadání, ale jsou trochu modifikovány pro event-driven model. Každá instance této třídy představuje
jednoho klienta. Po připojení nového klienta se vytvoří tato třída, která se pak stará o všechny akce vázané k jednomu klientovi.

```csharp
public interface IProtocol
{
    Task Start();
    Task Disconnect();
}
```
Jako entrypoint je zde zase metoda `Start`, která poté spustí komunikaci přes `ITransport`. Zprávy se zasílají pomocí obecného modelu `IBaseModel`, akce je pak dále specifikována podle typu modelu.

### Client

Třída která reprezentuje spojení jednoho klienta. Obsahuje v sobě rozhraní `IProtocol`, pro komunikaci, a poté si ukládá informace ke klientovi, jako je např. uživatelské jméno, nebo kanál ve kterém se zrovna nachází.

```csharp
public class Client
{
    public Ipk24ChatProtocol Protocol { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Channel { get; set; } = "general";
    public IPEndPoint? Address { get; set; }
}
```

### Server
Tato třída je už pak samotný server, který se stará o přijímání nových klientů.

Na začátku aplikace se spustí dva cykly, které přijímají nová spojení. 
```csharp
public async Task Run(Options options)
{
    await Task.WhenAll(RunTcp(), RunUdp());
}
```

V případě TCP protokolu je využita třída `TcpListener`, která pří navázaní nového TCP spojení vrátí přímo nový socket, ten se předá třídě `IProtocol`, a ta započne svůj běh jako task na pozadí.

```csharp
var socket = await server.AcceptTcpClientAsync(_cancellationTokenSource.Token);

var client = new Client();
_clients.Add(client);
```

V případě UDP neexistuje nic jako započetí komunikace, pouze se zasílají zprávy. Tudíž jsem si vytvořil jednoduchý mechanismus na přesměrování zpráv dle ip adresy a portu od odesílatele. Pokud na welcome socket dorazí zpráva, a její ip adresa + port nejsou přiřazeny ke klientovi, vytvoří se nový klient, který začně tuto adresu obsluhovat. Po úspešné autentikaci přestane klient využívat welcome socket, a vytvoří si nový socket na náhodném portu, takže přesměrování už pak nebude potřeba.

```csharp
if (client is not null)
{
    ServerLogger.LogDebug("Redirecting data to existing client");
    await ((UdpTransport)client.Protocol.Transport).Redirect(data);
    continue;
}

client = new Client();
_clients.Add(client);
```

 Server si uchovává seznam všech clientů, ať už pro přesměrování, tak také pro účely broadcastu. Ukončení činnosti klienta probíra v třídě `IProtocol`, poté je klient odstraněn ze seznamu, a činnost serveru pokračuje dál.

 ```csharp
private async Task Broadcast(MessageModel data, string channel, bool sendSelf = false)
{
    foreach (var client in _clients)
    {
        if ((!sendSelf && client.Protocol == _client.Protocol) || client.Channel != channel)
        {
            continue;
        }

        await client.Protocol.Message(data);
    }
}
```

## Testování
Vyzkoušel jsem si dva přístupy testování. Všechno testování bylo doprovázeno také programem Wireshark<sup>[6]</sup>. 

Testy byly v průběhu vývoje prováděny na mém osobním počítači, a před finálním odevzdáním také na referenčím virtuálním stroji, se systémem Ubuntu 23.10.

![Env specs](/ipk-project-2/IPK.Project2.App/Resources/specs.png "Specifications")

![Wireshark debug](/ipk-project-2/IPK.Project2.App/Resources/wireshark_communication.png "Wireshark")
*Wireshark logy z testu zmíněném v sekci o [E2E testech](#e2e-testování)*

### E2E testování
Jelikož jsem si vytvořil klienta jakožto IPK projekt č.1, a byl i slušeně bodově ohodnocen, tak jsem ho využil pro testy serveru. Ručně jsem si simuloval komunikaci mezi serverem a klientem, pro usnadnění hledání závad byl použit debugger.

Na obrázku je testování plnohodnotné komunikace. Na prvním obrázku je vidět komunikace mezi UDP a TCP klientem, z pohledu klienta. Je zde otestována autentikace, zasílání zpráv, přepojení do kanálu, a následne ukončení spojení jednoho klienta. Na druhém obrázku je pak vidět tato komunikace ze strany serveru.

![Client view](/ipk-project-2/IPK.Project2.App/Resources/client_e2e.png "Client view")
![Server view](/ipk-project-2/IPK.Project2.App/Resources/server_e2e.png "Server view")

### Unit testy
Validace modelů a příkazů byla otestována přes C# unit testy, pomocí frameworku `xUnit`<sup>[8]</sup>.

Test na edge case kdy by se uživatel snažil přihlásit se jménem které obsahuje diakritiku, což není povoleno. Očekávaným výstupem testu je vyhození chyby `ValidationException`. Podobné testy jsou vytvořené pro všechny model. Jsou sice základní a repetetivní, ale aspoň potom máme jistotu že v celé aplikaci se pracuje vždy s validními daty.
```csharp
    [Fact]
    public void AuthModel_DisplayNameInvalidCharacters()
    {
        // Arrange
        var model = new AuthModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            Secret = "1234-5678-abdc",
            Username = "pepa"
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
```

## Bibliografie

[RFC 793 - TCP]  Postel J., Request for Comments, RFC793 [online]. [cited 2024-04-22]. Available at: https://www.ietf.org/rfc/rfc0793.txt

[RFC 768 - UDP] Postel J., Request for Comments, RFC768 [online]. [cited 2024-04-22]. Available at: https://www.ietf.org/rfc/rfc768.txt

[Sockets] IBM documentation, Socket Addresses in TCP/IP [online]. [cited 2024-04-22]. Available at: https://www.ibm.com/docs/en/aix/7.1?topic=addresses-socket-in-tcpip

[RFCC 1180 - TCP/IP] Comer, D. E., Request for Comments, RFC1180 [online]. [cited 2024-04-22]. Available at: https://datatracker.ietf.org/doc/html/rfc1180

[Task Class] Microsoft Learn, Task Class (System.Threading.Tasks) [online]. [cited 2024-04-22]. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task?view=net-8.0

[Wireshark] Wireshark [online]. [cited 2024-04-22]. Available at: https://www.wireshark.org/

[Netcat] Netcat [online]. [cited 2024-04-22]. Available at: https://www.commandlinux.com/man-page/man1/nc.1.html

[Xunit] Xunit [online]. [cited 2024-04-22]. Available at: https://xunit.net/

[1]: https://www.ietf.org/rfc/rfc0793.txt
[2]: https://www.ietf.org/rfc/rfc768.txt
[3]: https://www.ibm.com/docs/en/aix/7.1?topic=addresses-socket-in-tcpip
[4]: https://datatracker.ietf.org/doc/html/rfc1180 
[5]: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task?view=net-8.0
[6]: https://www.wireshark.org/
[7]: https://www.commandlinux.com/man-page/man1/nc.1.html
[8]: https://xunit.net/
