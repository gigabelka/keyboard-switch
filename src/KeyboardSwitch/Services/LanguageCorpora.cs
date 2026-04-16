namespace KeyboardSwitch.Services;

/// <summary>
/// Small embedded word lists used to train the bigram models on startup.
/// Sources are standard top-frequency lists for each language; lower-case normalization applied at training.
/// The exact set need not be exhaustive — bigram statistics from ~200 frequent words generalize well.
/// </summary>
internal static class LanguageCorpora
{
    // ~200 top frequency English words (general corpus).
    public const string English =
        "the be to of and a in that have it for not on with he as you do at this but his by from they we " +
        "say her she or an will my one all would there their what so up out if about who get which go me " +
        "when make can like time no just him know take people into year your good some could them see other " +
        "than then now look only come its over think also back after use two how our work first well way " +
        "even new want because any these give day most us is are was were been being had has did does done " +
        "going made making seen got took gave given said found thought felt kept told came went left heard " +
        "called looked used world life water hand school house part place end home while again night point " +
        "right still small great might never under often without information something example through " +
        "different following around before since between always high next each every same system group " +
        "country problem company service government number program question fact area room story word " +
        "friend family together important public possible general open available special political personal " +
        "human social economic financial national local international common recent major current past future " +
        "learn teach read write speak listen understand remember forget answer question begin continue finish " +
        "create develop grow increase decrease change improve solve help support provide offer show describe";

    // ~200 top frequency Russian words.
    public const string Russian =
        "и в не он на я что тот быть с а весь это как она по но они к у ты из мы за вы так же от " +
        "сказать этот который мочь человек о один ещё бы такой только себя свой какой когда уже для " +
        "вот кто да говорить год знать мой до или если время рука нет самый ни стать большой даже другой " +
        "наш свои хотеть ему видеть мне вдруг под ли первый дело думать ну жизнь где потом очень со " +
        "хороший идти стоять ничего спросить его её ведь чтобы был была было были есть будет могу может " +
        "могут мог могла надо нужно должен должна нам вам им их тебе меня тебя моя мои твой твоя твои " +
        "ваш там тут сюда туда отсюда сегодня завтра вчера сейчас теперь всегда никогда иногда часто " +
        "редко мало много более менее совсем почти едва город страна мир место улица дом работа " +
        "слово глаз лицо голова сторона вопрос ответ сила власть народ ребёнок деньги машина книга " +
        "школа история минута день ночь утро вечер неделя месяц часть система партия правительство " +
        "компания служба проблема пример информация программа пример общий разный новый старый " +
        "молодой старший младший последний первый главный важный простой сложный большой маленький " +
        "хороший плохой белый чёрный красный зелёный синий жёлтый свет тёмный яркий тихий громкий " +
        "читать писать говорить слушать смотреть видеть знать понимать помнить забывать отвечать " +
        "спрашивать начинать продолжать заканчивать создавать развивать расти изменять улучшать " +
        "решать помогать поддерживать предоставлять показывать описывать " +
        "привет пока здравствуйте спасибо пожалуйста извините ладно хорошо плохо конечно возможно";

    // Alphabet size for smoothing (approximate): letters + apostrophe/hyphen.
    public const int EnglishAlphabet = 28;
    public const int RussianAlphabet = 34;
}
