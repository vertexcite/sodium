import nz.sodium.*;
import java.util.Optional;

public class Promise<A> {
    public Promise(Stream<A> sDeliver) {
        this.sDeliver = sDeliver.once();
        this.oValue = this.sDeliver.map(a -> Optional.of(a))
                                   .hold(Optional.empty());
    }
    private Promise(Cell<Optional<A>> oValue) {
        this.sDeliver = Stream.filterOptional(Operational.updates(oValue));
        this.oValue = oValue;
    }
    public final Stream<A> sDeliver;
    public final Cell<Optional<A>> oValue;
    public final Stream<A> then() {
        return Stream.filterOptional(Operational.value(oValue))
            .orElse(sDeliver).once();
    }
    public final void thenDo(Handler<A> h) {
        Transaction.runVoid(() ->
            then().listenOnce(h)
        );
    }
    public static <A,B,C> Promise<C> lift(final Lambda2<A, B, C> f,
                                          Promise<A> pa, Promise<B> pb) {
        return Transaction.run(() -> new Promise<C>(
            Cell.lift(
                (oa, ob) ->
                    oa.isPresent() && ob.isPresent()
                        ? Optional.of(f.apply(oa.get(), ob.get()))
                        : Optional.empty(),
                pa.oValue, pb.oValue)));
    }
}

