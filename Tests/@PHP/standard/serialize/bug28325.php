[expect php]
[file]
<?php

class a {
	public $b;
}
class b {
	public $c;
}
class c {
	public $d;
}
$a = new a();
$a->b = new b();
$a->b->c = new c();
$a->b->c->d = $a;
$s = serialize($a);
echo $s === serialize(unserialize($s)) ? "OK" : "FAILED";
?>