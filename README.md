PostfixCodeCompletion plugin for FlashDevelop
========================
[![Build status](https://ci.appveyor.com/api/projects/status/acnsq3sk2xboe3as?svg=true)](https://ci.appveyor.com/project/slavara/fd-postfix-code-completion-plugin)

The basic idea is to prevent caret jumps backwards while typing code.
Kind of surround templates on steroids baked with code completion.

## Minimum Requirements
* FlashDevelop 5.0.1 or never.
* Haxe 3.2.0 or never for haxe projects.

### Installation

Download the latest release. Open the .fdz file with FlashDevelop.

https://github.com/SlavaRa/fdplugin-postfix-code-completion/releases

## Features
Available templates for **AS3**:
* `.if` – checks boolean expression to be true `if (expr)`
* `.else` – checks boolean expression to be false `if (!expr)`
* `.null` – checks nullable expression to be null `if (expr == null)`
* `.notnull` – checks expression to be non-null `if (expr != null)`
* `.not` – negates value of inner boolean expression `!expr`
* `.foreach` – iterates over collection `foreach (var x in expr)`
* `.foin` - for Object surrounds with loop `for (var key:String in expr)`
* `.foin` - for Dictionary surrounds with loop `for (var key:Object in expr)`
* `.for` – for Array|Vector surrounds with loop `for (var i = 0; i < expr.length; i++)`
* `.for` – for Numeric surrounds with loop `for (var i = 0; i < expr; i++)`
* `.forr` – for Array|Vector reverse loop `for (var i = expr.length - 1; i >= 0; i--)`
* `.forr` – for Numeric reverse loop `for (var i = expr; i >= 0; i--)`
* `.var` – initialize new variable with expression `var x = expr;`
* `.const` – initialize new variable with expression `const x = expr;`
* `.new` – produces instantiation expression for type `new T()`
* `.par` – surrounds outer expression with parentheses `(expr)`
* `.return` – returns value from method/property `return expr;`
* `.while` – uses expression as loop condition `while (expr)`
* `.dowhile` – uses expression as loop condition  `do{...} while(expr);`
* `.sel` – selects expression in editor

Available templates for **Haxe**:
* `.code` - for String expression to be `expr.code`
* `.if` – checks boolean expression to be true  `if (expr)`
* `.else` – checks boolean expression to be false  `if (!expr)`
* `.null` – checks nullable expression to be null `if (expr == null)`
* `.notnull` – checks expression to be non-null `if (expr != null)`
* `.not` – negates value of inner boolean expression `!expr`
* `.foreach` – iterates over collection `for(it in expr`)
* `.for` – for Array|Vector|Iterator|Iterable surrounds with loop `for (i in 0...expr.Length)`
* `.for` – for Numeric surrounds with loop `for (i in 0...expr)`
* `.var` – initialize new variable with expression `var x = expr;`
* `.new` – produces instantiation expression for type `new T()`
* `.par` – surrounds outer expression with parentheses `(expr)`
* `.return` – returns value from method/property `return expr;`
* `.while` – uses expression as loop condition `while (expr)`
* `.dowhile` – uses expression as loop condition `do{...} while(expr);`
* `.sel` – selects expression in editor


## Как добавить сниппеты для постфиксного автокомплита самому?
Сниппеты для постфиксного автокомплита представляют из себя улучшенный вид стандартных сниппетов и по умолчанию распологаются в `FlashDevelop\Snippets\Language\postfixgenerators`, также в настройках плагина можно подключить пользовательские директории.

### Синтаксис
* `$(PCCMember)` - любая единица кода
* `$(PCCBoolean)` - логическое
* `$(PCCNullable)` - единица кода, которая может принимать значение null
* `$(PCCCollection)` - коллекция элементов, расположенных в памяти непосредственно друг за другом
* `$(PCCHash)` - ассоциативная коллекция
* `$(PCCNumber)` - числовое
* http://www.flashdevelop.org/wikidocs/index.php?title=Arguments
 
Маркеры можно использовать вместе используя разделитель `|`, например сниппет `if($(PCCBoolean|PCCNullable)$(EntryPoint))` будет работать как для логических так и для любых единиц кода, которые могут принимать значени null.
Кроме этого сниппеты поддерживают условия, для которых использутся разделитель `#pcc:PCCМаркер` или `#pcc:T`, например сниппет:
```
#pcc:PCCCollection
for (var $(ItmUniqueVar):int = 0; $(ItmUniqueVar) < $(PCCCollection).length; $(ItmUniqueVar)++) $(CSLB){
	$(EntryPoint)
}
#pcc:PCCNumber
for (var $(ItmUniqueVar):int = 0; $(ItmUniqueVar) < $(PCCNumber); $(ItmUniqueVar)++) $(CSLB){
	$(EntryPoint)
}
#pcc:flash.display.DisplayObjectContainer
for (var $(ItmUniqueVar):int = $(flash.display.DisplayObjectContainer).numChildren; $(ItmUniqueVar) >= 0; $(ItmUniqueVar)--) $(CSLB){
	$(EntryPoint)
}
```
развернется в код
для массива:
```
for (var i:int = 0; i < expr.length; i++) {
	|
}
```
для числовых:
```
for (var i:int = 0; i < expr; i++) {
	|
}
```
для `flash.display.DisplayObjectContainer` и наследников:
```
for (var i:int = expr.numChildren; i < expr; i++) {
	|
}
```
