# Projekt 1: Klient pro IPK24-CHAT server 
Autor: Dominik Huml <xhumld00@vutbr.cz>

## Teorie
Aplikace je klientem pro IPK24-CHAT server. Protokol má dvě varianty, první jako transportní protokol používá TCP<sup>[1]</sup> a druhá varianta používá UDP<sup>[2]</sup>
Pro navázání spojení je využit síťový socket<sup>[3]</sup>.

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
Je také efektivnější, protože nevytváří nové vlákno pro každý požadavek, ale využívá thread pool.

Aplikaci jsem rozdělil do více tříd, které se starají o různé části aplikace. Díky tomu je možné využít principu kompozice a injekce závislostí.
Hlavní části jsou `ChatClient`, `IProtocol`a `ITransport`. Pro přenos dat jsou pak specifikovány modely, které pak mohou být dále specializovány pro protokol.

![Flow diagram](/ipk-project-1/App/Resources/flow.png "Flow")

### Datové modely
Pro obecnou komunikaci je vyvořené rozhraní `IBaseModel`, které si pak implementují třídy dle typu zprávy (např. `AuthModel` pro AUTH zprávu). Tyto zprávy se pak předávají ve všech obecných rozhraních. Protokol UDP pak potřebuje nějaké data navíc (MessageID apod.), kvůli tomu vznikly modely pro UDP. Ty jsou sjednoceny přes rozhraní `IBaseUdpModel`. Tohle rozhraní dále implementuje funkce na binární serializaci a deserializaci, které pomocí reflexe zvládnou zpracovat libovolnou UDP třídu. Validace modelů probíhá přes annotační atributy.

```csharp
    [RegularExpression("[!-~]{1,20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
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
Tyto tranzice se snaží kopírovat konečný stavový automat ze zadání, ale jsou trochu modifikovány pro event-driven model.

```csharp
public interface IProtocol
{
    public event EventHandler<IBaseModel>? OnMessage;
    public event EventHandler? OnConnected;
    
    Task Start();
    Task Disconnect();
    Task Send(IBaseModel model);
}
```
Jako entrypoint je zde zase metoda `Start`, která poté spustí komunikaci přes `ITransport`. Zprávy se zasílají pomocí obecného modelu `IBaseModel`, akce je pak dále specifikována podle typu modelu.


### ChatClient
Tato třída je už pak samotná chatovací aplikace, která řídí běh programu. 

```csharp
public class ChatClient
{
    private readonly IProtocol _protocol;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IStandardInputReader _standardInputReader;
    private readonly SemaphoreSlim _connectedSignal = new(0, 1);
    
    private string _displayName = string.Empty;

    public ThreadSafeBool Finished { get; set; } = new(false);

    public ChatClient(IProtocol protocol, IStandardInputReader standardInputReader, CancellationTokenSource cancellationTokenSource)
    ...
}
```
Injektuje se zde rozhraní `IStandardInputReader`, které se pak stará o čtení vstupu ze standardního vstupu. Je to uděláno tímto způsobem pro snadnější testovatelnost. 
Klient pak také dostane specifikovaný protokol, a spustí běh aplikace.

```csharp
    try
    {
        await await Task.WhenAny(transportTask, stdinTask);
    }
```
Tím se začnou vykonávat dvě hlavní činnosti, to je asynchronní příjem zpráv od serveru, a synchronní příjem zpráv od uživatele. Oba tasky jsou implementovány jako nekonečné cykly,
s tím že přijímání zpráv od serveru je voláno asynchronně, a díky tomu není čekání blokující. V případě UDP pak na pozadí běží časovače, které po jejich vypršení zašlou event, a pokud není zpráva ješte potvrzena, tak ji buď znovu zašle, nebo informuje hlavní vlákno o chybě, a to pak ukončí přenos. Volání `Task.WhenAny` zajišťujě, že pokud jeden z tasků ukončí svoji činnost, ať úspěšně či ne, tak pak se v hlavním vlákkně
spustí exekuce zbytku programu, a ten se postará o ukončení všech tasků které běží na pozadí, pomocí tzv. CancellationTokenu, a vypíše výsledek uživatelu a řádně ukončí aplikaci.

## Testování
Vyzkoušel jsem si tři přístupy testování. Všechno testování bylo doprovázeno také programem Wireshark<sup>[6]</sup>. 

![Env specs](/ipk-project-1/App/Resources/specs.png "Specifications")
*Výpis z testovacího prostředí*

![Wireshark debug](/ipk-project-1/App/Resources/wireshark.png "Wireshark")
*Wireshark logy z testu zmíněném v sekci o [E2E testech](#e2e-testování)*

### Ruční testování
První pokusy o testování byly přes utilitu `netcat`<sup>[7]</sup>. Použil jsem ji pouze na TCP variantu, jelikož posílat ručně binární zprávy bylo značně komplikované.
Tohle testování jsem používal jen na začátku vývoje, poté jsem přešel k více sofistikovaným metodám

Zde je příklad testování základní autentikace. Vstupem je příkaz `/auth`, očekávaný výstup je oznámení o úspešné autentikaci.

![Netcat testovani](/ipk-project-1/App/Resources/netcat.png "Netcat")


### E2E testování
Pro usnadnění práce jsem si napsal TCP a UDP python server, který sice neuměl veškerou funkcionalitu, ale na otestování vetšiny věcí byl dostačující.
Testoval probíhaly z pohledu uživatele, to znamená psaní do konzole.

Na obrázku je testování plnohodnté komunikace. Na vstupu jsou všechny příkazy pro autentikaci, zasílání zpráv, a následne ukončení komunikace. Výstupem jsou potom tyto akce správně zalogované na serveru, a správně vypsané do konzole.

![Python server](/ipk-project-1/App/Resources/python-test.png "Python server")

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
Dále jsem se také pokusil udělat testy už větších částí projektu,
např. testy pro IProtocol rozhraní. Na ty jsem ještě využil knihovnu `NSubstitute`<sup>[9]</sup>, kterou jsem použil na mockování síťových věcí.

Tento test testuje zda funguje správně zadání příkazu pro autentikaci. Očekávaným výstupem je, že pres rozhraní protokol budou zaslána správně zparsovaná zpráva s autentikačním modelem. Závislosti klienta jsou mocnuté již zmíněnou knihovnou, aby testy byly izolované, a nezávisely např. na spojení se serverem. K tomu slouží E2E testy.
```csharp
	[Fact]
	public async Task Auth_Valid()
	{
		// Arrange
		var reader = Substitute.For<IStandardInputReader>();
		
		reader.ReadLine().Returns(
			_ => "/auth pepa 1234-5678-abcd Pepa_z_Brna", 
			_ => null
			);
		
		var (protocol, client) = ClientAuthSetup(reader);
		
		// Act
		var exitCode = await client.Start();
	  
		// Assert
		await protocol
			.Received()
			.Send(
				Arg.Is<AuthModel>(
					m => 
						m.DisplayName == "Pepa_z_Brna" && 
						m.Secret == "1234-5678-abcd" && 
						m.Username == "pepa"
						)
				);
		
		Assert.Equal(0, exitCode);
	}
```

Unit testy byly spoušteny v prostředí Rider

![Test output](/ipk-project-1/App/Resources/output.png "Test output")

## Bibliografie

1. Request for Comments, RFC793, Postel J. [RFC 793 - TCP](https://www.ietf.org/rfc/rfc0793.txt)
2. Request for Comments, RFC768, Postel J. [RFC 768 - UDP](https://www.ietf.org/rfc/rfc768.txt)
3. IBM documentation, Socket Addresses in TCP/IP [Sockets](https://www.ibm.com/docs/en/aix/7.1?topic=addresses-socket-in-tcpip)
4. Request for Comments, RFC1180, Comer, D. E. [RFCC 1180 - TCP/IP](https://datatracker.ietf.org/doc/html/rfc1180)
5. Microsoft Learn, Task Class (System.Threading.Tasks) [Task Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task?view=net-8.0)
6. Wireshark [Wireshark](https://www.wireshark.org/)
7. Netcat [Netcat](https://www.commandlinux.com/man-page/man1/nc.1.html)
8. Xunit [Xunit](https://xunit.net/)
9. NSubstitute [NSubstitute](khttps://nsubstitute.github.io/)

[1]: https://www.ietf.org/rfc/rfc0793.txt
[2]: https://www.ietf.org/rfc/rfc768.txt
[3]: https://www.ibm.com/docs/en/aix/7.1?topic=addresses-socket-in-tcpip
[4]: https://datatracker.ietf.org/doc/html/rfc1180 
[5]: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task?view=net-8.0
[6]: https://www.wireshark.org/
[7]: https://www.commandlinux.com/man-page/man1/nc.1.html
[8]: https://xunit.net/
[9]: https://nsubstitute.github.io/
