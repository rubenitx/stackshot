# Deja los literales de C# en ASCII: los caracteres no ASCII dentro de "..." y '...' pasan a escapes Unicode (barra invertida, u y 4 cifras); los comentarios no se tocan.
# Uso: perl tools/escape-literals.pl src/Fichero.cs   (o para todos: for f in $(find src tools -name "*.cs"); do perl tools/escape-literals.pl $f; done)
use strict; use warnings;
my $f = shift;
my $bs = chr(92); my $nl = chr(10);
open(my $in, '<:encoding(UTF-8)', $f) or die; local $/; my $src = <$in>; close $in;
my $out = ''; my $n = 0; my $i = 0; my $len = length $src;
my $esc = sub { my $c = shift; my $o = ord $c; return $c if $o < 128; $n++;
  if ($o > 0xFFFF) { $o -= 0x10000; return sprintf($bs . 'u%04X' . $bs . 'u%04X', 0xD800 + ($o >> 10), 0xDC00 + ($o & 0x3FF)); }
  return sprintf($bs . 'u%04X', $o); };
while ($i < $len) {
  my $c = substr($src, $i, 1);
  my $two = substr($src, $i, 2);
  if ($two eq '//') { my $e = index($src, $nl, $i); $e = $len if $e < 0; $out .= substr($src, $i, $e - $i); $i = $e; next; }
  if ($two eq '/*') { my $e = index($src, '*/', $i + 2); $e = $e < 0 ? $len : $e + 2; $out .= substr($src, $i, $e - $i); $i = $e; next; }
  if ($two eq '@"') { $out .= '@"'; $i += 2; while ($i < $len) { my $d = substr($src, $i, 1);
      if ($d eq '"') { if (substr($src, $i + 1, 1) eq '"') { $out .= '""'; $i += 2; next; } $out .= '"'; $i++; last; }
      $out .= $esc->($d); $i++; } next; }
  if ($c eq '"' || $c eq "'") { my $q = $c; $out .= $q; $i++; while ($i < $len) { my $d = substr($src, $i, 1);
      if ($d eq $bs) { $out .= substr($src, $i, 2); $i += 2; next; }
      if ($d eq $q) { $out .= $q; $i++; last; }
      if ($d eq $nl) { last; }
      $out .= $esc->($d); $i++; } next; }
  $out .= $c; $i++;
}
open(my $o, '>:encoding(UTF-8)', $f) or die; print $o $out; close $o;
print "escapados: $n\n";
