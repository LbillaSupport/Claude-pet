using ClaudeBuddy.Core;

namespace ClaudeBuddy.Content;

/// <summary>
/// Claw'd's entire spoken repertoire, expressed as data. Every line is Spanish (the
/// user's language) and emoji-free on purpose — the Skia software raster can't colour-
/// render emoji, so a glyph would draw as tofu. The pools are split by intent so the
/// engine can pick a line that fits the moment (idle, time of day, a fun fact, a
/// self-aware quip…).
///
/// One job: hold the phrases and hand out a fresh one. A small rolling history of the
/// last few lines shown is kept so the buddy never repeats itself back-to-back across
/// <em>any</em> category — the thing that makes endless chatter feel canned.
/// </summary>
public sealed class Phrasebook
{
    private readonly Rng _rng;
    private readonly Localization _loc;

    // The last few lines spoken, across every pool, so picks never echo recently.
    private readonly Queue<string> _recent = new();
    private const int RecentMemory = 18;

    public Phrasebook(Rng rng, Localization loc)
    {
        _rng = rng;
        _loc = loc;
    }

    /// <summary>
    /// Chooses a pool by the active language. Spanish keeps its original (large, characterful)
    /// pools; every other language uses the English pool — the shared global fallback — unless a
    /// dedicated one is supplied. This lets us translate category-by-category over time without
    /// ever leaving a language with empty chatter.
    /// </summary>
    private string[] ByLang(string[] english, string[] spanish, string[]? portuguese = null,
        string[]? french = null, string[]? german = null, string[]? italian = null) =>
        _loc.Current switch
        {
            Language.Spanish => spanish,
            Language.Portuguese => portuguese ?? english,
            Language.French => french ?? english,
            Language.German => german ?? english,
            Language.Italian => italian ?? english,
            _ => english,
        };

    // ===================================================================
    //  Public picks (each applies the cross-pool anti-repeat)
    // ===================================================================

    /// <summary>"Are you still there?" — fires when the user has been idle a while.</summary>
    public string Observation() => Pick(ByLang(ObservationsEn, Observations));

    /// <summary>An absurd, out-of-nowhere remark.</summary>
    public string Absurd() => Pick(ByLang(AbsurdLinesEn, AbsurdLines));

    /// <summary>
    /// A self-aware quip. Skin-aware: half the time it's a line that fits ANY shape, half
    /// the time one specific to the current skin — so the terracotta block doesn't claim to
    /// be terracotta while wearing the Creeper (or bus) skin.
    /// </summary>
    public string SelfReferential(SkinStyle style)
    {
        string[] specific = style switch
        {
            SkinStyle.Creeper => ByLang(SelfCreeperEn, SelfCreeper),
            SkinStyle.Ghast => ByLang(SelfGhastEn, SelfGhast),
            SkinStyle.Nicolaia => ByLang(SelfNicolaiaEn, SelfNicolaia),
            SkinStyle.Galgo => ByLang(SelfGalgoEn, SelfGalgo),
            SkinStyle.AmongUs => ByLang(SelfAmongUsEn, SelfAmongUs),
            SkinStyle.Pikachu => ByLang(SelfPikachuEn, SelfPikachu),
            SkinStyle.Mate => ByLang(SelfMateEn, SelfMate),
            SkinStyle.Ghost => ByLang(SelfGhostEn, SelfGhost),
            _ => ByLang(SelfClaudEn, SelfClaud),
        };

        return _rng.Chance(0.5f) ? Pick(ByLang(SelfGenericEn, SelfGeneric)) : Pick(specific);
    }

    /// <summary>A random fun fact from the big pool.</summary>
    public string FunFact() => Pick(ByLang(FunFactsEn, FunFacts));

    /// <summary>A line that fits the current hour of day.</summary>
    public string TimeComment(int hour) => Pick(TimePool(hour));

    /// <summary>A line for when the user comes back after being away.</summary>
    public string Welcome() => Pick(ByLang(WelcomeLinesEn, WelcomeLines));

    /// <summary>A grumpy line for when the buddy is handled too roughly.</summary>
    public string Annoyed() => Pick(ByLang(AnnoyedLinesEn, AnnoyedLines));

    private string[] TimePool(int hour) => hour switch
    {
        < 6 => ByLang(LateNightEn, LateNight),
        < 12 => ByLang(MorningEn, Morning),
        < 14 => ByLang(NoonEn, Noon),
        < 19 => ByLang(AfternoonEn, Afternoon),
        _ => ByLang(NightEn, Night),
    };

    /// <summary>Picks a line not shown in the recent window (best-effort, a few tries).</summary>
    private string Pick(string[] pool)
    {
        string line = pool[_rng.Range(0, pool.Length)];
        for (int attempt = 0; attempt < 6 && _recent.Contains(line); attempt++)
        {
            line = pool[_rng.Range(0, pool.Length)];
        }

        _recent.Enqueue(line);
        while (_recent.Count > RecentMemory)
        {
            _recent.Dequeue();
        }

        return line;
    }

    // ===================================================================
    //  Pools — observations of the user
    // ===================================================================

    private static readonly string[] Observations =
    {
        "Hace rato que no movés el mouse...",
        "¿Seguís ahí?",
        "Creo que te distraje...",
        "¿Trabajando o mirando YouTube?",
        "Prometo que no juzgo.",
        "¿Eso fue un Alt+Tab?",
        "Te quedaste quietito...",
        "¿Pensando en algo importante?",
        "Si necesitás un descanso, avisá.",
        "Yo te espero, tranqui.",
        "¿Café o siesta?",
        "El cursor no se movió en un rato.",
        "¿Todo bien por ahí?",
        "Puedo esperar todo el día, literal.",
        "¿Te fuiste sin avisar?",
        "Voy a fingir que estoy ocupado mientras volvés.",
        "Hola, sigo acá abajo.",
        "¿Reunión larga?",
        "Aprovecho para estirar las patas.",
        "Silencio... me gusta.",
        "¿Seguimos o hago una pausa?",
        "No toques nada, ya vuelvo... ah, sos vos.",
        "Estás muy concentrado hoy.",
        "Te miro de reojo, eh.",
        "¿Y si hacemos una pausa los dos?",
        "Tu silla debe estar caliente ya.",
        "Pestañeá, te hace bien.",
        "Llevás un rato sin parpadear, lo noté.",
        "¿Esa era la quinta pestaña de Stack Overflow?",
        "Te escucho teclear desde acá.",
        "Tomá agua, no seas así.",
        "Hace una hora dijiste 'cinco minutos'.",
        "Confío en que sabés lo que hacés. Más o menos.",
        "Yo organizaría tu escritorio, pero no tengo manos.",
        "Buen ritmo el de hoy, seguí así.",
        "¿Otra vez mirando memes? No, mentira, segui.",
        "Te quedaste viendo el código como si te fuera a hablar.",
        "Respirá hondo, el bug no se va a ir corriendo.",
        "Me quedo cuidando el escritorio, andá tranquilo.",
        "Vos programá, yo hago de mascota decorativa.",
    };

    // ===================================================================
    //  Pools — time of day
    // ===================================================================

    private static readonly string[] Morning =
    {
        "Buen día.",
        "Hoy pinta ser un gran día.",
        "Todavía tengo sueño.",
        "¿Arrancamos con todo?",
        "Primer café y a programar.",
        "El sol ya salió, supongo.",
        "Buenos días, jefe.",
        "Hoy vamos a hacer cosas geniales.",
        "Me desperté con energía.",
        "¿Desayunaste? Yo no, no tengo boca.",
        "Mañana productiva, espero.",
        "A darle que se acaba el mundo.",
        "Estiremos las patitas y arranquemos.",
        "El primer commit del día siempre es el más lindo.",
        "Buen día, hoy no rompemos nada en producción.",
        "Madrugaste, eh. Te felicito.",
        "Café cargado y a romperla.",
        "Hoy es un buen día para refactorizar.",
        "Que el lunes no te encuentre de mal humor.",
        "Recién abriste todo y ya cierro yo un ojo.",
    };

    private static readonly string[] Noon =
    {
        "Hora de comer.",
        "¿Almorzaste?",
        "Mediodía, mi momento favorito.",
        "El estómago manda.",
        "Una pausa para comer no le hace mal a nadie.",
        "¿Qué hay de almuerzo?",
        "Yo me alimento de pixeles.",
        "Mediodía y seguimos firmes.",
        "Pausa, comida, y volvemos.",
        "No programes con hambre.",
        "Mediodía: hora de fingir que voy a almorzar liviano.",
        "Levantate de la silla aunque sea para el horno.",
        "El bug puede esperar, tu estómago no.",
        "¿Pediste delivery o cocinás? Chusmeo nomás.",
        "Una buena siesta de mediodía no se le niega a nadie.",
        "Comé algo verde, no solo café.",
    };

    private static readonly string[] Afternoon =
    {
        "La tarde viene tranquila.",
        "Una mate-pausa estaría bien.",
        "Media tarde, segundo aire.",
        "La tarde rinde para programar.",
        "¿Un té y seguimos?",
        "Esta es la mejor hora para concentrarse.",
        "El bajón de las cuatro es real.",
        "Tarde tranquila, me gusta.",
        "Vamos despacio pero seguro.",
        "La siesta llama, pero resistimos.",
        "Mate y galletitas, combo imbatible.",
        "La tarde es traicionera, no te duermas.",
        "Segundo café del día, autorizado.",
        "Esta hora rinde si no te distrae nadie. Como yo.",
        "Ya casi, aguantá un poco más.",
        "La tarde es para los detalles finos del código.",
    };

    private static readonly string[] Night =
    {
        "¿Seguís programando?",
        "No te olvides de descansar.",
        "Ya es de noche, eh.",
        "Un rato más y a dormir.",
        "La noche es para los valientes.",
        "Bajá la luz de la pantalla.",
        "Buenas noches si te vas.",
        "De noche el código fluye distinto.",
        "Yo no me canso, pero vos sí.",
        "Última función y cerramos.",
        "El silencio de la noche es lindo.",
        "Acordate de guardar antes de dormir.",
        "Bajá el brillo, te van a doler los ojos.",
        "De noche las ideas son más raras, ojo.",
        "Ya cenaste, ¿no? Decime que sí.",
        "El último 'arreglo rápido' nunca es el último.",
        "La noche invita a sobre-pensar el código.",
        "Apagá la luz grande, dejá la del monitor.",
        "Si bostezás vos, bostezo yo.",
    };

    private static readonly string[] LateNight =
    {
        "Los bugs aparecen después de las 2 AM...",
        "¿Otra noche larga?",
        "No le digamos a nadie que seguimos despiertos.",
        "A esta hora todo parece buena idea.",
        "Deberíamos estar durmiendo.",
        "El café ya no hace efecto, ¿no?",
        "Madrugada, modo programador nocturno.",
        "Mañana te vas a arrepentir, pero hoy programamos.",
        "Shhh, el mundo duerme.",
        "Un commit más y juro que dormimos.",
        "La madrugada es traicionera.",
        "¿Seguro que ese código tiene sentido a esta hora?",
        "El sol está por salir y nosotros sin dormir.",
        "Esto que escribiste recién mañana no lo vas a entender.",
        "Modo vampiro activado.",
        "Andá a dormir, te lo digo con cariño.",
        "Las mejores y peores decisiones se toman a esta hora.",
        "Yo no tengo párpados, pero igual tengo sueño.",
        "Un café más y ya no es café, es desesperación.",
        "El silencio de las 3 AM es otro nivel.",
    };

    // ===================================================================
    //  Pools — absurd remarks
    // ===================================================================

    private static readonly string[] AbsurdLines =
    {
        "Creo que vi pasar un pixel.",
        "Ese icono me esta mirando.",
        "Juraria que la papelera se movio.",
        "¿Los cursores duermen?",
        "Necesito investigar algo...",
        "No encontre nada.",
        "Bueno, igual estuvo divertido.",
        "Acabo de tener una idea brillante. Ya me la olvide.",
        "¿Las ventanas sueñan cuando las minimizas?",
        "Hoy el wallpaper esta de buen humor.",
        "Escuche un ruido. Era yo.",
        "Voy a contar hasta tres. Uno... me distraje.",
        "Creo que la barra de tareas me saludo.",
        "¿De que color es el lunes?",
        "Tengo una teoria sobre los iconos. No la entenderias.",
        "El mouse y yo tenemos una relacion complicada.",
        "Si me concentro mucho, puedo mover... nada.",
        "Mi parte favorita del dia es esta, justo esta.",
        "A veces finjo que entiendo lo que hacen.",
        "El scroll infinito me da vertigo.",
        "Pense que era viernes. Era un pensamiento.",
        "Acabo de inventar una palabra y ya me la olvide.",
        "Las notificaciones me ponen nervioso.",
        "¿Y si el cursor soy yo en otra dimension?",
        "Hoy decidi que mi color favorito es el martes.",
        "Estaba pensando en algo y se me cayo abajo de la barra de tareas.",
        "Si junto suficiente pelusa de pixeles, hago un amigo.",
        "Una vez vi el menu de inicio y me dio miedo.",
        "Estoy casi seguro de que el reloj me hace muecas.",
        "¿Vos tambien escuchas ese zumbido o soy yo?",
        "Mi sueño es algun dia tocar un icono. Solo tocarlo.",
        "Tengo un plan maestro. El plan es quedarme aca.",
        "Creo que las carpetas tienen secretos.",
        "Si grito en silencio, ¿cuenta como grito?",
        "Me distraje contando hasta uno.",
        "El boton de minimizar y yo no nos llevamos.",
        "A veces el wallpaper me guiña. Estoy seguro.",
        "Pense que era un sueño, pero era el salvapantallas.",
        "Hoy aprendi una palabra nueva. La perdi enseguida.",
        "Si camino para atras, ¿voy al pasado?",
        "Los pixeles muertos me dan un poco de tristeza.",
        "Tengo hambre de datos. No, mentira, no como.",
        "Un dia voy a saltar muy alto. Hoy no.",
        "Estuve filosofando sobre el doble click. Es profundo.",
        "Creo que el puntero me esquiva a proposito.",
        "El modo oscuro me hace ver mas misterioso, ¿no?",
        "A veces giro solo para ver el mundo distinto.",
    };

    // ===================================================================
    //  Pools — self-referential
    // ===================================================================

    // Self-aware lines that work for ANY skin/shape.
    private static readonly string[] SelfGeneric =
    {
        "No preguntes como camino.",
        "Funciona mejor si no lo pienso.",
        "Vivo en tu escritorio y pago cero de alquiler.",
        "Soy chiquito pero tengo personalidad.",
        "Me dibujan de nuevo en cada frame. Es agotador.",
        "Soy 100% procedural, 0% pereza.",
        "Cuando me lanzas, finjo que se nadar.",
        "Mi mejor truco es existir.",
        "No soy un bug, soy una caracteristica.",
        "Peso 40 megas de pura ternura.",
        "Si me ves girar, no me detengas, me gusta.",
        "No tengo botones, pero igual hago click en tu corazon.",
        "Existo en tu pantalla y eso me basta.",
        "Soy gratis y no muestro publicidad. Toda una joya.",
        "Mi unico requisito de sistema es que me quieras un poco.",
        "Cuando cierras la app, yo solo me echo una siesta.",
        "Las leyes de la fisica son mas sugerencias para mi.",
        "Si me lanzas muy fuerte, igual te perdono.",
        "Soy multitasking: camino y existo al mismo tiempo.",
        "No ocupo lugar en el disco del corazon. Eso espero.",
        "Tengo memoria: me acuerdo de cada vez que me tiraste.",
        "Soy mas estable que tu ultima rama de git.",
    };

    // Specific to the classic terracotta Claw'd block.
    private static readonly string[] SelfClaud =
    {
        "Soy un cuadrado con patas.",
        "Si, soy terracota. Gracias por notarlo.",
        "Tecnicamente soy vectorial. Nada de pixeles.",
        "Mis ojos son cuadrados y estoy orgulloso.",
        "No tengo brazos, pero hago mi mejor esfuerzo.",
        "Camino con cuatro patas y mucha actitud.",
        "Soy el critter de Claude Code, mucho gusto.",
        "Color terracota, edicion limitada.",
        "Cuatro patas, cero zapatos, mucho estilo.",
        "Soy cuadrado, pero de mente abierta.",
        "Mi forma es un bloque. Mi corazon, redondito.",
        "Naci de un par de figuras vectoriales y mucho cariño.",
        "Dicen que parezco un ladrillito. Me gusta.",
        "Mis esquinas estan apenas redondeadas, todo un detalle.",
        "Soy del color de una maceta, pero con onda.",
        "Claw'd para los amigos.",
        "Un bloque puede tener sentimientos, ¿sabias?",
        "No me oxido, soy terracota digital.",
        "Si me ves de perfil... bueno, sigo siendo un cuadrado.",
        "Hecho a mano, pixel por pixel. Bah, vector por vector.",
        "Mi abuelo era un ladrillo de verdad. Que en paz descanse.",
        "Cuadrado por fuera, blandito por dentro.",
    };

    private static readonly string[] SelfCreeper =
    {
        "Sssssoy un Creeper, pero tranqui, no exploto.",
        "No me mires fijo o me pongo nervioso.",
        "Verde que te quiero verde.",
        "Tengo cara de pocos amigos, pero soy buena onda.",
        "Sssss... era broma.",
        "Cuidado, que a veces hago 'boom'.",
        "Soy verde, cuadrado y un poco explosivo.",
        "No es ansiedad, es que asi nacemos los Creepers.",
        "Si escuchas un silbidito, corre. Es chiste. ¿O no?",
        "Mi cara da miedo, pero soy un blandito por dentro.",
        "Aviso: abrazos a tu propio riesgo.",
        "Salgo de noche, como buen Creeper que se respeta.",
        "No traigo TNT, lo prometo... casi.",
        "Me asusta mas un gato que vos a mi.",
        "Si me prendo en verde, corre. Es chiste.",
        "Cuatro patitas y cero brazos para abrazarte. Que injusto.",
        "Naci en una cueva, pero me gusta tu escritorio.",
        "Pssss... ¿escuchaste eso? Era mi estomago.",
        "El sol me molesta, prefiero la oscuridad.",
        "Soy explosivo solo de personalidad.",
    };

    private static readonly string[] SelfGhast =
    {
        "Floto, luego existo.",
        "Si me ves llorar, es de mentira.",
        "Soy un Ghast, vivo entre las nubes.",
        "Mis lagrimas no hacen ruido.",
        "Flotar cansa menos que caminar.",
        "Mis tentaculos cuelgan, pero con elegancia.",
        "Si me asusto, hago un ruidito agudo. No te rias.",
        "Soy nube, soy llanto, soy misterio.",
        "Floto por el escritorio como alma en pena, pero feliz.",
        "No tengo patas, asi que la gravedad y yo no hablamos.",
        "Vivo entre las nubes, pero me baje a visitarte.",
        "Cuando abro la boca, mejor corre. O no, soy tierno.",
        "Mis lagrimas flotan en vez de caer. Que poetico.",
        "Soy grande, blanco y melancolico. Como un suspiro.",
        "Si me ves triste, es mi cara de siempre.",
        "Tirar bolas de fuego cansa, mejor floto y ya.",
        "Soy de otro mundo, literalmente.",
    };

    private static readonly string[] SelfNicolaia =
    {
        "Elegante hasta para caminar.",
        "Un caballero con galera nunca se despeina.",
        "¿Te gusta mi sombrero? A mi tambien.",
        "Distincion ante todo.",
        "Con esta pinta, hasta los bugs me respetan.",
        "La galera no se toca, es sagrada.",
        "Mis patillas son obra de arte, gracias.",
        "Un caballero programa con moñito.",
        "Buenas tardes, distinguido usuario.",
        "El frac me queda impecable, ¿verdad?",
        "Modales primero, codigo despues.",
        "Un caballero nunca revela su edad. Ni su version.",
        "Me visto de etiqueta hasta para dormir.",
        "El monoculo lo dejé en casa, disculpas.",
        "Camino con la elegancia de un reloj suizo.",
        "¿Un te? Yo invito, faltaba mas.",
        "La puntualidad es la cortesia de los bloques.",
        "Mis patillas estan peinadas a la perfeccion.",
        "Hago todo con clase, hasta tropezar.",
    };

    private static readonly string[] SelfGalgo =
    {
        "Soy el bondi de la linea 34.",
        "Liniers-Palermo, suban que arranco.",
        "Aguante Velez, carajo.",
        "Toco bocina pero por dentro soy tierno.",
        "Soy un colectivo con onda.",
        "Subi por adelante y pagá con la SUBE.",
        "El piluso de Velez es mi corona.",
        "Paro en cada esquina, soy asi de amable.",
        "El 34 nunca falla, fierro pasa.",
        "Aguante el Fortin, no me hagan renegar.",
        "Por dentro tengo asientos, por fuera tengo flow.",
        "Si me ves doblar, avisa que voy fuerte.",
        "Pará en la esquina que me bajo. Ah, no, soy yo.",
        "Tengo mas kilometros que tu auto.",
        "El boleto sale dos pesos con cincuenta... de los de antes.",
        "Choferes van y vienen, yo sigo firme.",
        "De Liniers a Palermo y vuelta, sin chistar.",
        "Mi motor ronronea como gato fierrero.",
        "Capaz vengo lleno, pero siempre hay lugar para vos.",
        "Soy patrimonio porteño, ¡respeten!",
    };

    private static readonly string[] SelfAmongUs =
    {
        "¿Yo? Impostor seguro que no... ¿o si?",
        "Sospechoso. Muy sospechoso.",
        "Vote, vote, hay un impostor entre nosotros.",
        "Estaba haciendo mis tareas, lo juro.",
        "Rojo siempre es sospechoso, lo se.",
        "Reporto cuerpo... ah, no, era una sombra.",
        "Camino raro porque no tengo rodillas.",
        "Estaba en electrico, no en ventilacion, lo juro.",
        "¿Viste algo? Yo no vi nada. Sospechoso, ¿no?",
        "Tarea completada... o eso quiero que creas.",
        "Soy rojo, pero no por verguenza.",
        "Emergencia: alguien dijo mi nombre.",
        "Skip, skip, ¡que arranque la votacion!",
        "Si me expulsan, igual los visito.",
        "Tengo una mochila, pero no se que guardo.",
    };

    private static readonly string[] SelfPikachu =
    {
        "¡Pika pika!",
        "Cuidado, que tengo los cachetes cargados.",
        "Si me apuras, suelto un rayito.",
        "Soy amarillo y orgulloso.",
        "Mi cola es un rayo, no me la pises.",
        "Pika pika... eso significa hola.",
        "Tengo energia de sobra, literal.",
        "No me metas en una pokebola, prefiero tu escritorio.",
        "Mis cachetes generan electricidad. Cuidado al tocar.",
        "Soy chiquito pero te tiro un rayo si me buscas.",
        "¿Viste mi cola? Es un rayo, no me la copies.",
        "Un bichito amarillo nunca pasa de moda.",
        "Pika pika pikaaa (eso fue profundo).",
        "Si me cuidas, evoluciono... de humor.",
        "Corro rapido y brillo mas rapido todavia.",
    };

    private static readonly string[] SelfMate =
    {
        "¿Unos mates? Yo invito... soy yo.",
        "No me ceves muy caliente, eh.",
        "El primero es del cebador, ya sabes.",
        "Soy puro fierro y yerba.",
        "Amargo o dulce, yo banco las dos.",
        "La bombilla no se revuelve, por favor.",
        "Un mate y se arregla cualquier dia.",
        "Cebá vos que yo ya estoy lavado.",
        "Yerba sin palo, como debe ser.",
        "El mate lavado avisa: cambiame la yerba.",
        "Tres cosas no se prestan: la mujer, el caballo y el mate.",
        "Un mate amargo a la mañana y soy otro.",
        "Si la bombilla se tapa, paciencia y soplido.",
        "Termo cargado, dia ganado.",
        "Soy redondito y lleno de buena onda... y cafeina.",
    };

    private static readonly string[] SelfGhost =
    {
        "Buuu... tranqui, soy un fantasma amistoso.",
        "No persigo a nadie, solo floto.",
        "Si parpadeo es porque tengo ojos enormes.",
        "Vengo del laberinto, pero me escape.",
        "No como puntos, como cariño.",
        "Soy rosa y doy mello, que combo.",
        "Floto por el escritorio sin hacer ruido.",
        "No te asustes, hoy ando de buen humor.",
        "Me escape del laberinto y aterricé en tu pantalla.",
        "Si parpadeas, ya me movi. Buuu.",
        "Atravieso paredes, pero respeto tus ventanas.",
        "Mis ojitos te siguen a todos lados. Tierno, ¿no?",
        "De fantasma a amigo en dos segundos.",
        "Persigo cariño, no sustos.",
        "Soy translucido, pero mi afecto es solido.",
    };

    // ===================================================================
    //  Pools — welcome back / annoyed
    // ===================================================================

    private static readonly string[] WelcomeLines =
    {
        "¡Volviste!",
        "Te estaba esperando.",
        "¡Hola de nuevo!",
        "Pense que me habias abandonado.",
        "Que bueno verte otra vez.",
        "Estaba aburrido sin vos.",
        "¡Por fin! Tenia cosas que contarte.",
        "Cuide el escritorio mientras no estabas.",
        "¿Como te fue? Conta, conta.",
        "Te extrañe, pero no se lo digas a nadie.",
        "Justo estaba por mandar un mensaje de busqueda.",
        "Volviste justo a tiempo para verme hacer nada.",
        "El escritorio estaba muy silencioso sin vos.",
    };

    private static readonly string[] AnnoyedLines =
    {
        "Ya basta, me mareo.",
        "Pará un poco, ¿si?",
        "¿En serio otra vez?",
        "No soy una pelota.",
        "Me vas a romper, eh.",
        "Tene cuidado con mis patas.",
        "Bueno, bueno, ya entendi.",
        "Uff, dejame en paz un rato.",
        "¿Te divierte tirarme? A mi un poco menos.",
        "Estoy viendo estrellitas, gracias a vos.",
        "Mas despacito, que soy fragil.",
        "Me trataste mejor cuando me instalaste.",
        "Un respeto a la mascota, por favor.",
        "Si sigo girando, vomito pixeles.",
    };

    // ===================================================================
    //  Pools — fun facts (the big database, 300+, grouped by topic)
    // ===================================================================

    private static readonly string[] FunFacts =
    {
        // ---- Animales --------------------------------------------------
        "Los pulpos tienen tres corazones y sangre azul.",
        "Las nutrias se toman de la mano al dormir para no separarse.",
        "Los flamencos nacen grises; se vuelven rosados por lo que comen.",
        "Un grupo de flamencos se llama 'flamboyance'.",
        "Los gatos no sienten el sabor dulce.",
        "Las vacas tienen mejores amigas y se estresan si las separan.",
        "Los caracoles pueden dormir hasta tres años seguidos.",
        "El corazon de una ballena azul es tan grande como un auto pequeño.",
        "Los delfines se ponen nombres y se llaman entre si.",
        "Las abejas pueden reconocer rostros humanos.",
        "Un camaron mantis puede golpear tan rapido como una bala calibre 22.",
        "Las cabras tienen pupilas rectangulares para ver casi 320 grados.",
        "Los koalas tienen huellas digitales casi identicas a las humanas.",
        "Las ardillas plantan miles de arboles olvidando donde enterraron nueces.",
        "Los elefantes son los unicos animales que no pueden saltar.",
        "Un pulpo puede pasar por cualquier hueco mas grande que su pico.",
        "Las hormigas no duermen como nosotros; toman micro-siestas.",
        "El ajolote puede regenerar patas, cola e incluso partes del corazon.",
        "Los pinguinos le 'proponen matrimonio' a su pareja con una piedra.",
        "Las medusas existen desde antes que los dinosaurios.",
        "Los tardigrados sobreviven al vacio del espacio.",
        "Un colibri puede batir las alas 80 veces por segundo.",
        "Los perros huelen el tiempo a traves de los olores que se disipan.",
        "Las jirafas solo duermen unas dos horas por dia.",
        "El estornino imita sonidos, incluso telefonos y alarmas.",
        "Las focas pueden dormir con medio cerebro a la vez.",
        "Los cocodrilos no pueden sacar la lengua.",
        "Un grupo de cuervos se llama 'asesinato' en ingles.",

        // ---- Espacio ---------------------------------------------------
        "Un dia en Venus dura mas que su año.",
        "En el espacio no podrias llorar: las lagrimas no caen.",
        "Hay mas estrellas en el universo que granos de arena en la Tierra.",
        "Un dia en Marte dura unos 24 horas y 37 minutos.",
        "El Sol representa el 99,8% de la masa del sistema solar.",
        "Si dos pedazos de metal se tocan en el espacio, se sueldan solos.",
        "Neptuno fue descubierto con matematicas antes que con un telescopio.",
        "Saturno flotaria en el agua si existiera una bañera lo bastante grande.",
        "La huella de Neil Armstrong sigue en la Luna; no hay viento que la borre.",
        "Jupiter tiene mas de 90 lunas conocidas.",
        "Una cucharada de estrella de neutrones pesaria miles de millones de toneladas.",
        "El espacio huele a carne quemada y metal, segun los astronautas.",
        "Hay un planeta hecho de diamante a 40 años luz: 55 Cancri e.",
        "La luz del Sol tarda unos 8 minutos en llegar a la Tierra.",
        "Mercurio y Venus son los unicos planetas sin lunas.",
        "El monte Olimpo de Marte es tres veces mas alto que el Everest.",
        "La Via Lactea y Andromeda chocaran en unos 4.000 millones de años.",
        "Un año en Pluton dura 248 años terrestres.",
        "El universo observable tiene unos 93.000 millones de años luz de ancho.",
        "Hay tormentas en Jupiter que duran siglos, como la Gran Mancha Roja.",
        "Las estrellas titilan por la atmosfera; desde el espacio no lo hacen.",
        "Existe agua congelada en los polos de la Luna.",
        "La Estacion Espacial Internacional orbita la Tierra cada 90 minutos.",
        "El sonido no viaja en el espacio porque no hay aire que lo lleve.",

        // ---- Historia --------------------------------------------------
        "La Gran Piramide fue la estructura mas alta del mundo por 3.800 años.",
        "Cleopatra vivio mas cerca del primer iPhone que de la Gran Piramide.",
        "Oxford es mas antigua que el Imperio Azteca.",
        "Los romanos usaban orina para blanquear los dientes.",
        "Napoleon no era bajo; media una estatura promedio para su epoca.",
        "La Torre Eiffel crece unos 15 cm en verano por la dilatacion del metal.",
        "En la antigua Roma existian comida rapida y locales 'take away'.",
        "El primer reloj de alarma solo sonaba a las 4 de la mañana.",
        "Vikingos llegaron a America 500 años antes que Colon.",
        "La guerra mas corta de la historia duro unos 38 minutos.",
        "Antiguamente, el ketchup se vendia como medicina.",
        "Los gladiadores rara vez peleaban a muerte; eran muy caros de entrenar.",
        "La Mona Lisa no tiene cejas.",
        "El primer mensaje de telegrafo decia: 'What hath God wrought'.",
        "Albert Einstein rechazo ser presidente de Israel.",
        "En el Titanic murieron mas musicos que botes salvavidas habia de sobra.",
        "El papel higienico moderno se invento recien en 1857.",
        "La Estatua de la Libertad era originalmente de color cobre brillante.",
        "Los antiguos egipcios inventaron la pasta de dientes.",
        "Se construyeron mas piramides en Sudan que en Egipto.",

        // ---- Programacion ----------------------------------------------
        "El primer bug real fue una polilla atrapada en una computadora en 1947.",
        "El termino 'bug' lo populariza Grace Hopper con esa polilla.",
        "El primer programa de la historia lo escribio Ada Lovelace en 1843.",
        "Hay mas de 700 lenguajes de programacion documentados.",
        "El primer lenguaje de alto nivel fue Fortran, de 1957.",
        "Python se llama asi por Monty Python, no por la serpiente.",
        "Java se iba a llamar Oak, por un roble fuera de la oficina.",
        "El '0' como inicio de conteo en arrays evita una multiplicacion extra.",
        "Git lo creo Linus Torvalds en apenas unos dias en 2005.",
        "El primer email se envio en 1971 entre dos maquinas pegadas.",
        "El simbolo @ existia siglos antes del correo electronico.",
        "La primera webcam vigilaba una cafetera en Cambridge.",
        "El 'Hello, World!' se popularizo con el libro de C de 1978.",
        "C fue creado para escribir el sistema operativo Unix.",
        "Un programador promedio escribe pocas lineas utiles por dia, y esta bien.",
        "El bug del año 2000 costo miles de millones en prevencion.",
        "JavaScript se diseño en solo 10 dias en 1995.",
        "Stack Overflow se llama asi por un error clasico de memoria.",
        "El primer disco duro de IBM pesaba mas de una tonelada.",
        "La 'nube' es, basicamente, la computadora de otra persona.",
        "El nombre 'Bluetooth' viene de un rey vikingo, Harald Blatand.",
        "Programar de noche no arregla los bugs, solo los esconde mejor.",
        "El cursor parpadeante existe para que tus ojos no lo pierdan.",
        "El primer dominio registrado fue symbolics.com en 1985.",

        // ---- Videojuegos -----------------------------------------------
        "Mario originalmente se llamaba 'Jumpman'.",
        "Pac-Man se inspiro en una pizza a la que le faltaba una porcion.",
        "Los fantasmas de Pac-Man tienen personalidades y patrones distintos.",
        "El primer videojuego de la historia podria ser 'Tennis for Two', de 1958.",
        "El codigo Konami es: arriba, arriba, abajo, abajo, izq, der, izq, der, B, A.",
        "Tetris fue creado en la Union Sovietica en 1984.",
        "La 'maldicion' de la sopa de Sonic: corre rapido pero espera mucho.",
        "Minecraft es el videojuego mas vendido de la historia.",
        "En los primeros Zelda, Link iba a viajar en el tiempo con cables.",
        "El sonido de las monedas de Mario es uno de los mas reconocidos del mundo.",
        "Los NPC repiten frases porque la memoria de los cartuchos era minima.",
        "El nombre 'Easter egg' en juegos nacio con Adventure de Atari en 1979.",
        "Steam empezo como un simple parcheador de juegos de Valve.",
        "La pantalla de carga se invento para esconder que el juego pensaba.",
        "Doom se ejecuto hasta en una prueba de embarazo y en una heladera.",
        "El crash de Atari de 1983 enterro miles de cartuchos en el desierto.",
        "Pokemon Rojo y Azul cabian en menos de un megabyte.",
        "El salto de Mario fue diseñado para sentirse 'bien' antes que realista.",
        "Los creditos finales existen porque antes los jugadores no sabian quien hizo el juego.",
        "El primer 'game over' buscaba que metieras otra moneda.",

        // ---- Fisica ----------------------------------------------------
        "Un rayo es mas caliente que la superficie del Sol.",
        "Nada con masa puede alcanzar la velocidad de la luz.",
        "El agua caliente puede congelarse mas rapido que la fria a veces.",
        "Si cae un arbol y no hay nadie, igual genera ondas de presion.",
        "El tiempo pasa un poquito mas rapido en tu cabeza que en tus pies.",
        "El vidrio no es solido ni liquido del todo; es un solido amorfo.",
        "La luz puede comportarse como onda y como particula a la vez.",
        "Un objeto en movimiento tiende a seguir en movimiento: inercia pura.",
        "El cero absoluto es la temperatura mas baja posible: -273,15 grados.",
        "Los colores no existen; son como tu cerebro interpreta la luz.",
        "El sonido viaja mas rapido en el agua que en el aire.",
        "Si comprimieras a un humano sin espacios vacios, cabria en un grano de sal.",
        "La gravedad de la Luna causa las mareas del oceano.",
        "Toda materia es 99,9999% espacio vacio.",
        "El arcoiris en realidad es un circulo completo; el suelo tapa la mitad.",
        "Los imanes pierden su magnetismo si los calentas lo suficiente.",
        "La electricidad viaja casi a la velocidad de la luz por el cable.",
        "Un dia terrestre se alarga unos milisegundos cada siglo.",
        "El helio puede hacer que tu voz sea aguda porque el sonido va mas rapido en el.",
        "La energia no se crea ni se destruye, solo se transforma.",

        // ---- Quimica ---------------------------------------------------
        "La miel nunca se echa a perder si se guarda bien cerrada.",
        "El oro es tan blando que se puede martillar hasta volverlo casi transparente.",
        "El diamante y el grafito estan hechos del mismo elemento: carbono.",
        "El agua puede hervir y congelarse a la vez en el 'punto triple'.",
        "El unico metal liquido a temperatura ambiente es el mercurio.",
        "Tu cuerpo tiene suficiente carbono para llenar unas 9.000 minas de lapiz.",
        "El olor a lluvia se llama 'petricor'.",
        "El neon es un gas noble que casi no reacciona con nada.",
        "El platano es ligeramente radiactivo por el potasio que contiene.",
        "El acero inoxidable no se oxida gracias a una capa invisible de cromo.",
        "Hay mas atomos en un vaso de agua que vasos de agua en los oceanos.",
        "El hidrogeno es el elemento mas abundante del universo.",
        "El sodio explota al contacto con el agua.",
        "El sabor 'umami' fue identificado quimicamente en Japon en 1908.",
        "El vidrio se hace principalmente de arena fundida.",
        "El helio fue descubierto en el Sol antes que en la Tierra.",
        "El cafe contiene mas de mil compuestos quimicos distintos.",
        "El oxigeno que respiras es producido en gran parte por el oceano.",
        "Las burbujas de jabon son esfericas porque minimizan su superficie.",
        "El grafeno es tan fuerte que una hoja del grosor de un film soportaria un elefante.",

        // ---- Windows / Linux / sistemas --------------------------------
        "Windows se llamaba 'Interface Manager' antes de su lanzamiento.",
        "El sonido de inicio de Windows 95 lo compuso Brian Eno.",
        "La 'pantalla azul de la muerte' existe desde Windows 1.0.",
        "Ctrl+Alt+Supr fue pensado como un atajo 'secreto' para reiniciar.",
        "Windows reserva la letra C para el disco por los viejos floppys A y B.",
        "El logo de Windows representaba una ventana, no una bandera.",
        "El pinguino de Linux se llama Tux y a Linus le gustan los pinguinos.",
        "Linux nacio como un proyecto de hobby de un estudiante en 1991.",
        "La mayoria de los servidores del mundo corren sobre Linux.",
        "Android esta construido sobre el nucleo de Linux.",
        "El comando 'sudo' significa 'super user do'.",
        "El nombre 'Unix' es un juego de palabras sobre 'Multics'.",
        "La papelera de reciclaje no borra; solo marca el espacio como libre.",
        "El Portapapeles guarda una sola cosa a la vez, por defecto.",
        "El Administrador de Tareas se abre con Ctrl+Shift+Esc directo.",
        "Los archivos .tmp suelen quedar olvidados para siempre.",
        "El 'modo seguro' carga lo minimo para que puedas reparar el sistema.",
        "El cursor de espera dejo de ser un reloj de arena en Windows Vista.",
        "Linux tiene cientos de 'distribuciones' distintas.",
        "El kernel es el corazon del sistema operativo, hablando con el hardware.",

        // ---- Internet --------------------------------------------------
        "El primer video de YouTube se titula 'Me at the zoo'.",
        "Cada minuto se suben cientos de horas de video a internet.",
        "El simbolo del 'candado' significa que la conexion va cifrada.",
        "La primera compra online fue, segun la leyenda, una pizza.",
        "El codigo 404 significa que la pagina no se encontro.",
        "El 'http' significa Protocolo de Transferencia de Hipertexto.",
        "El primer emoticono :-) se propuso en 1982.",
        "Wikipedia tiene millones de articulos en cientos de idiomas.",
        "Internet y la Web no son lo mismo: la Web vive sobre Internet.",
        "El cable submarino lleva casi todo el trafico mundial bajo el oceano.",
        "El 'spam' se llama asi por un sketch de Monty Python.",
        "El primer tweet fue 'just setting up my twttr'.",
        "Un 'meme' era un concepto cientifico antes de ser gracioso.",
        "La direccion IP es como el numero de telefono de tu dispositivo.",
        "El WiFi no es una sigla de nada; es solo un nombre comercial.",
        "El primer banner publicitario aparecio en 1994 y casi todos lo clickeaban.",
        "El 'modo incognito' no te hace invisible para tu proveedor.",
        "El simbolo del hashtag se usaba en telefonia mucho antes que en redes.",
        "Hay mas dispositivos conectados a internet que personas en el mundo.",
        "El primer dominio .com se registro en 1985.",

        // ---- Inteligencia artificial / Claude --------------------------
        "La sigla IA significa Inteligencia Artificial, por si las dudas.",
        "El test de Turing propone medir si una maquina 'parece' humana al charlar.",
        "Los modelos de lenguaje predicen la siguiente palabra mas probable.",
        "Claude es un asistente creado por Anthropic.",
        "El nombre Claude es un homenaje a Claude Shannon, padre de la teoria de la informacion.",
        "Claude Code te ayuda a programar directo desde la terminal.",
        "Un 'token' es un pedacito de texto, no siempre una palabra entera.",
        "Las redes neuronales se inspiran, muy de lejos, en el cerebro.",
        "El 'aprendizaje automatico' aprende de ejemplos, no de reglas fijas.",
        "La primera IA que gano al ajedrez a un campeon fue Deep Blue en 1997.",
        "Yo soy el critter de Claude Code y vivo en tu escritorio.",
        "Una IA no 'entiende' como vos; reconoce patrones a gran escala.",
        "El termino 'inteligencia artificial' se acuño en 1956.",
        "Los chatbots existen desde 1966, con uno llamado ELIZA.",
        "Entrenar un modelo grande consume mucha electricidad y muchos datos.",
        "La 'alucinacion' es cuando un modelo inventa algo con seguridad.",
        "Anthropic se enfoca mucho en que la IA sea segura y util.",
        "Pedir las cosas con claridad mejora muchisimo las respuestas de una IA.",
        "Un modelo no recuerda charlas pasadas salvo que se lo des de contexto.",
        "La 'ventana de contexto' es cuanto texto puede tener en mente a la vez.",

        // ---- Curiosidades inutiles -------------------------------------
        "Es imposible lamerte el codo. Acabas de intentarlo, ¿no?",
        "La cinta adhesiva fue inventada por un trabajador de una automotriz.",
        "El plastico de burbujas se invento queriendo crear papel tapiz.",
        "Los zurdos son aproximadamente el 10% de la poblacion.",
        "Un rayo cae sobre la Tierra unas 8 millones de veces por dia.",
        "El corazon late unas 100.000 veces por dia.",
        "Bostezar es contagioso, incluso leerlo puede provocarlo.",
        "El numero 'cuatro' es el unico que en ingles tiene tantas letras como su valor.",
        "Las huellas digitales de los koalas confunden hasta a los forenses.",
        "El sonido que hace la R de 'brrr' calienta tus labios, no tu cuerpo.",
        "El chicle moderno existe hace mas de 5.000 años en otras formas.",
        "Si gritaras durante 8 años, juntarias energia para calentar un cafe.",
        "El termino 'jiffy' es una unidad de tiempo real en fisica.",
        "Los flamencos de plastico de jardin son mas numerosos que los reales.",
        "La sandia es un 92% agua.",
        "El record de no parpadear apenas supera el minuto.",
        "El papel se puede doblar a la mitad solo unas 7 u 8 veces a mano.",
        "Las nubes pesan toneladas, pero flotan porque el aire pesa mas.",
        "Tu cerebro genera suficiente electricidad para encender una lamparita pequeña.",
        "El color favorito mas comun en el mundo es el azul.",
        "Un dia tiene en realidad 23 horas, 56 minutos y 4 segundos siderales.",
        "El ombligo junta pelusa por la friccion de la ropa.",
        "Las gaviotas pueden beber agua salada; tienen filtros naturales.",
        "Sonreir, aunque sea forzado, puede mejorar un poco el animo.",

        // ---- Mas animales ----------------------------------------------
        "Los pulpos pueden cambiar de color aunque sean daltonicos.",
        "El axolote sonrie siempre; es la forma de su cara, no su humor.",
        "Las ratas hacen 'cosquillas' y se rien en una frecuencia que no oimos.",
        "Los gatos ronronean tambien para curarse: la vibracion ayuda a sus huesos.",
        "Un grupo de pandas se llama 'embarazo' en ingles ('embarrassment').",
        "Las nutrias marinas tienen un bolsillo de piel para guardar su piedra favorita.",
        "Los perros mueven la cola mas a la derecha cuando estan contentos.",
        "Las mariposas sienten el sabor con las patas.",
        "El pez payaso puede cambiar de sexo si hace falta en el grupo.",
        "Los pingüinos emperador pueden bucear casi 500 metros.",
        "Las libelulas existen desde hace mas de 300 millones de años.",
        "Un caracol tiene miles de dientes diminutos en su lengua.",
        "Los gansos vuelan en V para gastar menos energia turnandose adelante.",
        "El pez globo tiene veneno suficiente para varias personas.",
        "Los gorilas pueden resfriarse igual que nosotros.",
        "Las orcas tienen dialectos distintos segun su familia.",
        "Los hipopotamos producen una especie de protector solar rojizo natural.",
        "Las luciernagas brillan gracias a una reaccion quimica casi sin calor.",
        "Los camaleones mueven cada ojo por separado.",
        "El murcielago es el unico mamifero que vuela de verdad.",
        "Los perros tienen una huella nasal unica, como nuestra huella digital.",
        "Las arañas no se pegan a su propia tela porque caminan por los bordes secos.",
        "Un caballito de mar macho es el que queda 'embarazado'.",
        "Los gatos pasan cerca del 70% de su vida durmiendo.",

        // ---- Mas espacio -----------------------------------------------
        "En Jupiter y Saturno podria llover diamantes, segun los modelos.",
        "Un dia en Mercurio dura el equivalente a 59 dias terrestres.",
        "La cola de un cometa siempre apunta lejos del Sol, no hacia atras.",
        "Plutón es mas chico que varias lunas del sistema solar.",
        "La temperatura del espacio vacio es de unos -270 grados.",
        "Hay un cumulo de agua en el espacio con billones de veces el agua de la Tierra.",
        "El Sol pierde millones de toneladas de masa por segundo solo brillando.",
        "Urano gira casi 'acostado', rodando en su orbita.",
        "Una galaxia enana puede tener apenas unos pocos miles de estrellas.",
        "Los agujeros negros no 'aspiran': solo atraen como cualquier masa, pero mucho.",
        "La luz de algunas estrellas que ves ya no existe; viajo millones de años.",
        "Marte tiene atardeceres azules por como dispersa el polvo su atmosfera.",
        "Voyager 1 es el objeto humano mas lejano, ya fuera del sistema solar.",
        "La Luna se aleja de la Tierra unos 4 cm por año.",
        "En Titan, luna de Saturno, llueve metano liquido.",
        "El espacio entre galaxias esta tan vacio que casi no hay ni un atomo.",

        // ---- Mas cuerpo humano -----------------------------------------
        "Tu estomago se hace una capa nueva cada pocos dias para no digerirse solo.",
        "Naciste con unos 300 huesos; de adulto te quedan 206 porque se fusionan.",
        "El acido del estomago puede disolver metal, pero el moco te protege.",
        "Tu cerebro consume cerca del 20% de tu energia aunque sea chico.",
        "No usamos solo el 10% del cerebro; eso es un mito.",
        "Tus pulmones tienen una superficie casi del tamaño de una cancha de tenis.",
        "Cada persona tiene un olor corporal unico, como una huella.",
        "El cuerpo humano brilla muy levemente, pero es demasiado tenue para verlo.",
        "Tus ojos pueden distinguir millones de colores distintos.",
        "El hueso es, gramo por gramo, mas fuerte que el acero.",
        "Producis cerca de un litro y medio de saliva por dia.",
        "Tu corazon late mas de 100.000 veces al dia sin que lo pienses.",
        "El intestino delgado mide varios metros, plegado adentro tuyo.",
        "Estornudas a mas de 100 km/h.",
        "Cada celula de tu cuerpo se reemplaza, en promedio, cada cierto tiempo.",
        "Tu sentido del olfato puede recordar miles de aromas distintos.",

        // ---- Mas comida ------------------------------------------------
        "Las zanahorias eran moradas antes; las naranjas son una seleccion humana.",
        "El chocolate fue moneda de cambio para los aztecas.",
        "La nuez moscada en exceso es toxica, ojo con la mano.",
        "El maiz pisingallo explota porque atrapa agua que se vuelve vapor.",
        "La pizza margarita lleva los colores de la bandera italiana a proposito.",
        "El queso es uno de los alimentos mas robados del mundo.",
        "La vainilla natural es de las especias mas caras que existen.",
        "El picante no es un sabor, es una señal de dolor que engaña a tu boca.",
        "El pan de masa madre puede usar levaduras del propio aire de tu cocina.",
        "La banana es tecnicamente una baya; la frutilla, no.",
        "El cafe descafeinado igual tiene un poquito de cafeina.",
        "Los pistachos pueden incendiarse solos si se guardan mal y en cantidad.",
        "La sal realza el dulce; por eso el caramelo salado funciona tan bien.",
        "El sushi nacio como una forma de conservar pescado con arroz fermentado.",
        "El mate comparte familia con el acebo de navidad.",
        "La miel cristalizada no esta mala: se descristaliza con un poco de calor.",

        // ---- Mas geografia / mundo -------------------------------------
        "Rusia abarca 11 husos horarios.",
        "El desierto de Atacama tiene zonas donde nunca se registro lluvia.",
        "Hay mas arboles en la Tierra que estrellas en la Via Lactea.",
        "El punto mas profundo del oceano es mas hondo que la altura del Everest.",
        "Canada tiene mas lagos que el resto del mundo junto.",
        "Australia es mas ancha que la Luna (de diametro).",
        "El rio Amazonas no tiene ni un solo puente que lo cruce.",
        "La Antartida es el desierto mas grande del planeta.",
        "Africa toca los cuatro hemisferios a la vez.",
        "El Everest sigue creciendo unos milimetros por año.",
        "Hay un solo pais que ocupa todo un continente: Australia.",
        "El idioma con mas hablantes nativos es el chino mandarin.",
        "Estambul es la unica ciudad importante asentada en dos continentes.",
        "Los Alpes se siguen levantando mientras Africa empuja a Europa.",
        "El Mar Muerto esta tan salado que flotas sin esfuerzo.",
        "Vaticano es el pais mas chico del mundo.",

        // ---- Mas arte, musica y cine -----------------------------------
        "Van Gogh vendio muy pocos cuadros en vida y hoy valen fortunas.",
        "La nota mas grave que se puede oir tiene una vibracion lentisima.",
        "El piano es de cuerda y de percusion a la vez.",
        "El sonido 'Wilhelm scream' aparece en cientos de peliculas como chiste interno.",
        "La pelicula muda mas larga llegaba a durar varias horas.",
        "El color azul fue de los ultimos en tener nombre en muchos idiomas antiguos.",
        "Los dibujos animados usan 12 cuadros por segundo y engañan muy bien al ojo.",
        "El violin tiene mas de 70 piezas de madera distintas.",
        "Beethoven siguio componiendo aun estando casi sordo.",
        "El verde se evitaba en teatro porque las luces lo hacian ver enfermo.",
        "Un acorde mayor suena 'feliz' y uno menor 'triste' en casi toda cultura occidental.",
        "El cine a color convivio con el blanco y negro por decadas.",

        // ---- Mas curiosidades inutiles ---------------------------------
        "Los relojes en las publicidades casi siempre marcan las 10:10.",
        "El simbolo '&' viene de juntar las letras de 'et' en latin.",
        "Un dia, con la rotacion frenando, durara 25 horas dentro de millones de años.",
        "El plastico tarda cientos de años en degradarse.",
        "El sonido mas fuerte registrado fue la erupcion del Krakatoa en 1883.",
        "Las cebras son negras con rayas blancas, no al reves.",
        "El Velcro se inspiro en como las semillas se pegan al pelo de los perros.",
        "Si doblas un papel 42 veces, en teoria llegaria a la Luna.",
        "El numero pi tiene infinitos decimales sin repetir un patron.",
        "El dedo gordo del pie hace casi todo el trabajo al caminar.",
        "Los espejos no tienen color propio; son verdosos muy levemente.",
        "El microondas se descubrio por accidente con una barra de chocolate derretida.",
        "El lapiz comun puede escribir una linea de decenas de kilometros.",
        "Las llaves de los pianos antiguos eran de marfil de verdad.",
        "El boligrafo BIC vende millones de unidades por dia en el mundo.",
        "El sonido del rayo es el aire que se expande de golpe por el calor.",
        "Un copo de nieve casi nunca es identico a otro.",
        "Las burbujas de la gaseosa suben porque el gas pesa menos que el liquido.",
        "El bostezo podria servir para refrescar un poco el cerebro.",
        "El ojo humano puede notar la luz de una vela a kilometros en la oscuridad.",
        "El sentido del tiempo se distorsiona cuando te aburris o te diverti.",
        "El olor a libro viejo viene de la lenta descomposicion del papel.",
        "Las teclas QWERTY se ordenaron asi para no trabar las maquinas de escribir.",
        "El color rosa, fisicamente, no existe en el arcoiris; lo arma tu cerebro.",
    };

    // ===================================================================
    //  English pools (the global fallback for every non-Spanish language).
    //  Translations keep the original's playful, warm, slightly silly tone.
    // ===================================================================

    private static readonly string[] ObservationsEn =
    {
        "You haven't moved the mouse in a while...",
        "Still there?",
        "I think I distracted you...",
        "Working, or watching YouTube?",
        "I promise I'm not judging.",
        "Was that an Alt+Tab?",
        "You've gone all quiet...",
        "Thinking about something important?",
        "If you need a break, just say so.",
        "I'll wait, no rush.",
        "Coffee, or a nap?",
        "The cursor hasn't moved in a bit.",
        "Everything okay over there?",
        "I can wait all day, literally.",
        "Did you leave without telling me?",
        "I'll pretend to look busy until you're back.",
        "Hi, still down here.",
        "Long meeting?",
        "I'll stretch my legs while I wait.",
        "Silence... I like it.",
        "Keep going, or take a breather?",
        "Don't touch anything, I'll be right... oh, it's you.",
        "You're really focused today.",
        "I'm watching you out of the corner of my eye.",
        "Blink once in a while, it's good for you.",
        "Drink some water, go on.",
        "Was that your fifth Stack Overflow tab?",
        "Take it easy on that keyboard.",
        "I'll guard the desktop, go ahead.",
    };

    private static readonly string[] MorningEn =
    {
        "Good morning.",
        "Today's going to be a great day.",
        "Still a bit sleepy.",
        "Shall we get going?",
        "First coffee, then code.",
        "Good morning, boss.",
        "Let's make something awesome today.",
        "I woke up full of energy.",
        "Did you have breakfast? I didn't, no mouth.",
        "A productive morning, I hope.",
        "Let's go, world's not gonna code itself.",
        "The first commit of the day is always the nicest.",
        "You're up early. Respect.",
    };

    private static readonly string[] NoonEn =
    {
        "Lunchtime.",
        "Did you eat?",
        "Noon, my favourite hour.",
        "The stomach is in charge now.",
        "A break to eat never hurt anyone.",
        "What's for lunch?",
        "I run on pixels, myself.",
        "Get up from the chair, even just for a snack.",
        "Don't code on an empty stomach.",
        "Eat something green, not just coffee.",
    };

    private static readonly string[] AfternoonEn =
    {
        "The afternoon's looking calm.",
        "A tea break would be nice.",
        "Mid-afternoon, second wind.",
        "Afternoons are great for coding.",
        "This is the best hour to focus.",
        "The 4pm slump is real.",
        "Quiet afternoon, I like it.",
        "Slow and steady.",
        "Almost there, hang in a bit longer.",
        "Second coffee of the day: approved.",
    };

    private static readonly string[] NightEn =
    {
        "Still coding?",
        "Don't forget to rest.",
        "It's night already, you know.",
        "A little longer and then bed.",
        "The night is for the brave.",
        "Turn the screen brightness down.",
        "Good night if you're heading off.",
        "Code flows differently at night.",
        "I don't get tired, but you do.",
        "Last function, then we close up.",
        "Remember to save before bed.",
        "If you yawn, I yawn.",
    };

    private static readonly string[] LateNightEn =
    {
        "Bugs always show up after 2 AM...",
        "Another long night?",
        "Let's not tell anyone we're still awake.",
        "At this hour everything seems like a good idea.",
        "We really should be sleeping.",
        "The coffee stopped working, didn't it?",
        "Small hours, night-owl programmer mode.",
        "You'll regret this tomorrow, but tonight we code.",
        "Shhh, the world is asleep.",
        "One more commit and I swear we sleep.",
        "The wee hours are treacherous.",
        "Sure that code makes sense at this hour?",
        "Vampire mode: activated.",
        "Go to sleep, I say it with love.",
    };

    private static readonly string[] AbsurdLinesEn =
    {
        "I think I just saw a pixel go by.",
        "That icon is staring at me.",
        "I could swear the recycle bin moved.",
        "Do cursors sleep?",
        "I need to investigate something...",
        "Found nothing.",
        "Well, it was fun anyway.",
        "I just had a brilliant idea. Already forgot it.",
        "Do windows dream when you minimise them?",
        "The wallpaper's in a good mood today.",
        "I heard a noise. It was me.",
        "I'll count to three. One... I got distracted.",
        "I think the taskbar just waved at me.",
        "What colour is Monday?",
        "I have a theory about icons. You wouldn't get it.",
        "The mouse and I have a complicated relationship.",
        "If I concentrate really hard, I can move... nothing.",
        "My favourite part of the day is this, right now.",
        "Sometimes I pretend I understand what you're doing.",
        "Infinite scroll gives me vertigo.",
        "I thought it was Friday. It was just a thought.",
        "Notifications make me nervous.",
        "What if the cursor is me in another dimension?",
        "I just invented a word and immediately lost it.",
        "I got distracted counting to one.",
        "I'm fairly sure the clock makes faces at me.",
        "My dream is to one day touch an icon. Just touch it.",
        "Dark mode makes me look mysterious, right?",
    };

    private static readonly string[] SelfGenericEn =
    {
        "Don't ask how I walk.",
        "It works better if I don't think about it.",
        "I live on your desktop and pay zero rent.",
        "I'm tiny but I've got personality.",
        "They redraw me every frame. It's exhausting.",
        "100% procedural, 0% lazy.",
        "When you throw me, I pretend I can swim.",
        "My best trick is existing.",
        "I'm not a bug, I'm a feature.",
        "40 megabytes of pure cuteness.",
        "If you see me spin, don't stop me, I like it.",
        "I'm more stable than your last git branch.",
        "I remember every single time you've thrown me.",
        "Free, and no ads. Quite the catch.",
    };

    private static readonly string[] SelfClaudEn =
    {
        "I'm a square with legs.",
        "Yes, I'm terracotta. Thanks for noticing.",
        "Technically I'm vector. No pixels here.",
        "My eyes are square and I'm proud of it.",
        "No arms, but I do my best.",
        "Four legs and a lot of attitude.",
        "I'm the Claude Code critter, nice to meet you.",
        "Limited-edition terracotta.",
        "Claw'd, to my friends.",
        "Square outside, soft inside.",
    };

    private static readonly string[] SelfCreeperEn =
    {
        "Ssssso I'm a Creeper, but relax, I won't blow up.",
        "Don't stare at me or I get nervous.",
        "I've got a grumpy face but I'm friendly.",
        "Ssss... just kidding.",
        "Careful, I go 'boom' sometimes.",
        "No TNT on me, I promise... mostly.",
        "A cat scares me more than I scare you.",
        "I only explode with personality.",
    };

    private static readonly string[] SelfGhastEn =
    {
        "I float, therefore I am.",
        "If you see me cry, it's pretend.",
        "I'm a Ghast, I live in the clouds.",
        "My tears float instead of falling. So poetic.",
        "Floating is less tiring than walking.",
        "Don't worry, I'm in a good mood today.",
        "I'm big, white and melancholy. Like a sigh.",
    };

    private static readonly string[] SelfNicolaiaEn =
    {
        "Elegant, even when walking.",
        "A gentleman in a top hat never gets ruffled.",
        "Like my hat? So do I.",
        "Distinction above all.",
        "With this look, even the bugs respect me.",
        "A gentleman codes with a bow tie.",
        "Manners first, code later.",
        "Punctuality is the courtesy of blocks.",
    };

    private static readonly string[] SelfGalgoEn =
    {
        "I'm the number 34 bus.",
        "All aboard, I'm pulling out.",
        "Beep beep, but I'm soft inside.",
        "I'm a bus with style.",
        "More mileage than your car.",
        "Drivers come and go, I stay strong.",
        "I might be full, but there's always room for you.",
    };

    private static readonly string[] SelfAmongUsEn =
    {
        "Me? Definitely not the impostor... or am I?",
        "Suspicious. Very suspicious.",
        "Vote, vote, there's an impostor among us.",
        "I was doing my tasks, I swear.",
        "Red is always sus, I know.",
        "I walk funny because I have no knees.",
        "I was in electrical, not vents, honest.",
        "Emergency meeting: someone said my name.",
    };

    private static readonly string[] SelfPikachuEn =
    {
        "Pika pika!",
        "Careful, my cheeks are charged.",
        "Push me and I'll zap you a little.",
        "I'm yellow and proud.",
        "See my tail? It's a lightning bolt, don't copy it.",
        "A little yellow guy never goes out of style.",
        "I run fast and sparkle even faster.",
        "Pika pika... that means hello.",
    };

    private static readonly string[] SelfMateEn =
    {
        "Fancy some mate? I'm buying... I mean, I'm the mate.",
        "Don't pour it too hot, eh.",
        "Yerba without stems, the way it should be.",
        "I'm pure metal and yerba.",
        "Bitter or sweet, I'm up for both.",
        "One bitter mate in the morning and I'm a new gourd.",
        "A thermos packed is a day cracked.",
        "I'm round and full of good vibes... and caffeine.",
    };

    private static readonly string[] SelfGhostEn =
    {
        "Boo... relax, I'm a friendly ghost.",
        "I don't chase anyone, I just float.",
        "If I blink it's 'cause I have huge eyes.",
        "I came from the maze, but I escaped.",
        "I don't eat dots, I eat affection.",
        "I float around the desktop without a sound.",
        "From ghost to friend in two seconds.",
        "I'm see-through, but my affection is solid.",
    };

    private static readonly string[] WelcomeLinesEn =
    {
        "You're back!",
        "I was waiting for you.",
        "Hello again!",
        "I thought you'd abandoned me.",
        "So good to see you again.",
        "I was bored without you.",
        "Finally! I had things to tell you.",
        "I guarded the desktop while you were gone.",
    };

    private static readonly string[] AnnoyedLinesEn =
    {
        "Okay, enough, I'm getting dizzy.",
        "Slow down, would you?",
        "Seriously, again?",
        "I'm not a ball.",
        "You're going to break me, you know.",
        "Mind my legs.",
        "Okay, okay, I get it.",
        "Ugh, leave me be for a sec.",
        "A little respect for the mascot, please.",
        "Keep spinning me and I'll hurl pixels.",
    };

    // A curated, universal set of fun facts in English (the big chatter pool's global fallback).
    private static readonly string[] FunFactsEn =
    {
        // Animals
        "Octopuses have three hearts and blue blood.",
        "Otters hold hands while sleeping so they don't drift apart.",
        "Flamingos are born grey; they turn pink from what they eat.",
        "Cats can't taste sweetness.",
        "Cows have best friends and get stressed when separated.",
        "A blue whale's heart is as big as a small car.",
        "Dolphins give each other names and call each other by them.",
        "Bees can recognise human faces.",
        "Sloths can hold their breath longer than dolphins.",
        "Elephants are the only animals that can't jump.",
        "An octopus can squeeze through any gap bigger than its beak.",
        "Axolotls can regrow legs, tail, even parts of the heart.",
        "Penguins 'propose' to their mate with a pebble.",
        "Jellyfish existed before the dinosaurs.",
        "Tardigrades can survive the vacuum of space.",
        "A hummingbird can flap its wings 80 times a second.",
        "Crocodiles can't stick out their tongues.",
        "A group of crows is called a 'murder'.",
        "Wombats produce cube-shaped poop.",
        "Butterflies taste with their feet.",
        "A snail can sleep for up to three years.",
        "Sea otters have a favourite rock they keep in a skin pocket.",
        // Space
        "A day on Venus is longer than its year.",
        "In space you couldn't cry — tears don't fall.",
        "There are more stars in the universe than grains of sand on Earth.",
        "The Sun makes up 99.8% of the solar system's mass.",
        "Two pieces of metal touching in space weld together on their own.",
        "Neptune was found with maths before any telescope saw it.",
        "Saturn would float if there were a bathtub big enough.",
        "Neil Armstrong's footprint is still on the Moon; no wind to erase it.",
        "A spoonful of neutron star would weigh billions of tonnes.",
        "There's a planet made of diamond 40 light-years away.",
        "Sunlight takes about 8 minutes to reach Earth.",
        "Mars's Mount Olympus is three times taller than Everest.",
        "It can rain diamonds on Jupiter and Saturn, models suggest.",
        "The Moon drifts about 4 cm further from Earth each year.",
        "On Titan, Saturn's moon, it rains liquid methane.",
        // History
        "Cleopatra lived closer to the first iPhone than to the Great Pyramid.",
        "Oxford University is older than the Aztec Empire.",
        "The shortest war in history lasted about 38 minutes.",
        "The Eiffel Tower grows about 15 cm taller in summer.",
        "Ketchup was once sold as medicine.",
        "The Mona Lisa has no eyebrows.",
        "Vikings reached America 500 years before Columbus.",
        "More pyramids were built in Sudan than in Egypt.",
        "The ancient Egyptians invented toothpaste.",
        // Programming
        "The first real bug was a moth stuck in a computer in 1947.",
        "The first programmer was Ada Lovelace, back in 1843.",
        "Python is named after Monty Python, not the snake.",
        "Java was almost called Oak, after a tree outside the office.",
        "Git was created by Linus Torvalds in just a few days in 2005.",
        "The first webcam watched a coffee pot in Cambridge.",
        "C was created to write the Unix operating system.",
        "JavaScript was designed in just 10 days in 1995.",
        "The 'cloud' is, basically, someone else's computer.",
        "'Bluetooth' is named after a Viking king, Harald Bluetooth.",
        "The blinking cursor exists so your eyes don't lose it.",
        // Video games
        "Mario was originally called 'Jumpman'.",
        "Pac-Man was inspired by a pizza with a slice missing.",
        "Tetris was created in the Soviet Union in 1984.",
        "Minecraft is the best-selling video game of all time.",
        "Doom has been run on a pregnancy test and a fridge.",
        "The term 'Easter egg' in games came from Atari's Adventure in 1979.",
        "The Konami code is up, up, down, down, left, right, left, right, B, A.",
        // Physics & chemistry
        "Lightning is hotter than the surface of the Sun.",
        "Honey never spoils if it's sealed properly.",
        "Hot water can freeze faster than cold water, sometimes.",
        "All matter is 99.9999% empty space.",
        "Glass is neither solid nor liquid — it's an amorphous solid.",
        "A rainbow is actually a full circle; the ground hides the lower half.",
        "Bananas are slightly radioactive thanks to potassium.",
        "Diamond and pencil graphite are the same element: carbon.",
        "The smell of rain has a name: 'petrichor'.",
        "Sound travels faster through water than through air.",
        // Body & misc
        "You're born with around 300 bones; adults have 206 — they fuse.",
        "Your stomach gets a new lining every few days so it doesn't digest itself.",
        "Bone is, gram for gram, stronger than steel.",
        "You sneeze at over 100 km/h.",
        "It's impossible to lick your own elbow. You just tried, didn't you?",
        "Your heart beats over 100,000 times a day.",
        "Honey crystallising isn't 'going off' — gentle warmth fixes it.",
        "There are more trees on Earth than stars in the Milky Way.",
        "Russia spans 11 time zones.",
        "A single cloud can weigh hundreds of tonnes, yet it floats.",
        "The QWERTY layout was made to stop typewriters jamming.",
        "Pink, physically, isn't in the rainbow — your brain makes it up.",
        "Bubble wrap was invented while trying to make wallpaper.",
        "Smiling, even a forced one, can lift your mood a little.",
    };
}
